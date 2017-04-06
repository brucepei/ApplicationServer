package POE::Component::AS;

use warnings;
use strict;
use FindBin;
use File::Spec;
use Data::Dumper;
use XML::Simple;
use POE;
use POE::Wheel::SocketFactory;
use POE::Wheel::ReadWrite;
use POE::Component::Pool::Thread;
use Logger;
use Capture::Tiny 'capture';
use JSON;
#use threads;
#use threads::shared;

our $VERSION = eval '0.0001';

sub get_logger {
    my $package = shift || __PACKAGE__;
    Logger->is_init ? Logger->new : (die "Please init Logger module before use $package!");
}

sub parse_conf {
    my ( $config_file ) = @_;
    die "Failed to find configure file '$config_file', $!" unless -e $config_file;
    my $config = XMLin( $config_file );
    #print Dumper( $config );
    my $local_ip = $config->{App}->{Local_IP};
    die "Config->App->Local_IP should be a local ip address!" unless $local_ip && $local_ip =~ /^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$/;
    my $local_port = $config->{App}->{Local_Port};
    die "Config->App->Local_Port should be a decimal digit!" unless $local_port && $local_port =~ /^\d+$/;

    return {
        local_port     => $local_port,
        local_ip       => $local_ip,
    }
}

### Run in child threads BEGIN ###
sub run_received_command {
    my ($sender, $args_json) = @_;
    my $log = get_logger();
    my ($args, $json_result);
    eval {
        $args = decode_json($args_json);
    };
    if( $@ ) {
        $log->debug("Failed to parse command: '$args_json'!");
        $json_result = encode_json( {stdout => undef, stderr => $@, exit => -1} );
    } else {
        $log->debug("Begin to run command: $args->{command} from session $sender!");
        my ($out, $err, $ret) = capture {
            system($args->{command});
        };
        $json_result = encode_json( {stdout => $out, stderr => $err, exit => $ret} );
        $log->debug("End to run command: $args->{command} from session $sender!");
    }
    return ($sender, $json_result);
}

### Run in child threads END ###

### POE inline state BEGIN ###
sub on_complete_command {
    my ($kernel, $sender, $json_result) = @_[ KERNEL, ARG0, ARG1 ];
    my $log = get_logger();
    $log->debug("Cet command result for session $sender: '$json_result'!");
    $kernel->post($sender => on_client_command_done => $json_result);
}

sub create {
    my $class = shift;
    my %args  = @_;
    my $self  = bless \%args, $class;

    $self->{config} ||= 'shell.xml';
    $self->{alias}  ||= 'as';
    unless( $self->{config} =~ /[\/\/]/ ) {
        $self->{config} = File::Spec->catfile( $FindBin::Bin, $self->{config} ); #add the cwd path
    }
    my $config = parse_conf( $self->{config} );
    foreach ( keys %{$config} ) {
        $self->{$_} = $config->{$_};
    }
    POE::Component::Pool::Thread->new(
        MinFree       => 2,
        MaxFree       => 5,
        MaxThreads    => 15,
        StartThreads  => 2,
        Name          => "thread_pool",
        EntryPoint    => \&run_received_command,
        CallBack      => \&on_complete_command,
    );
    
    POE::Session->create(
        inline_states => {
            _start => sub { #not start automatically, and need user code to post 'start' event!
                $_[KERNEL]->alias_set($self->{alias});
                $_[HEAP]->{config} = $config;
            },
            _default => sub {
                my ($kernel, $event, $args) = @_[KERNEL, ARG0, ARG1];
                unless( $event eq '_child' ) {
                    my $log = get_logger();
                    local $" = "', '";
                    $args = join "', '", map { defined( $_ ) ? $_ : 'undef' } @$args;
                    $log->debug( "Unknown event '$event' in session '$self->{alias}' with args: '$args'!" );
                }
            }
        },
        object_states => [
            $self => [ qw/start stop on_server_error on_server_accept on_client_connect/ ],
        ],
    );
    return $self;
}

sub start {
    my $log = get_logger();
    my $port = $_[HEAP]->{config}->{local_port};
    $_[HEAP]->{server} = POE::Wheel::SocketFactory->new(
        BindPort     => $port,
        SuccessEvent => "on_server_accept",
        FailureEvent => "on_server_error",
        Reuse        => 'yes',
    );
    $_[HEAP]->{clients} = {};
    #$_[KERNEL]->sig(INT => "got_sig_int");
    $log->debug( "start to listen at port $port!" );
}

sub stop {
    $_[KERNEL]->signal( $_[KERNEL], "SIG_Exit" ); #broadcast SIG_Exit to all clients, each client has 'SIG_Exit' signal
    delete $_[HEAP]->{server};
}

### POE inline state END ###

### POE object state BEGIN ###
sub on_server_accept {
    my ( $self, $kernel, $heap, $client_socket, $peeraddr, $peerport) = @_[OBJECT, KERNEL, HEAP, ARG0..ARG2];
    my $log = get_logger();
    $peeraddr =~ s/./sprintf("%d.", ord($&))/esg;
    chop($peeraddr);
    my $remote_addr = "$peeraddr:$peerport";
    $log->info("SERVER $$: recieved a connection from $remote_addr");
    $kernel->yield(on_client_connect => $remote_addr, $client_socket);
}

sub on_server_error {
    my ($operation, $errnum, $errstr) = @_[ARG0, ARG1, ARG2];
    my $log = get_logger();
    $log->error( "Server $operation error $errnum: $errstr" );
    delete $_[HEAP]{server};
}

sub on_client_connect {
    my ( $self, $kernel, $heap, $client_addr, $client_socket) = @_[OBJECT, KERNEL, HEAP, ARG0..$#_];
    my $log = get_logger();
    $log->debug("Client '$client_addr' connected, start receving command!");
    my $config = $heap->{config};
    POE::Session->create(
        inline_states => {
            _start  => sub {
                $_[HEAP]->{rw} = POE::Wheel::ReadWrite->new(
                    Handle       => $client_socket,
                    InputEvent   => "on_client_command",
                    ErrorEvent   => "on_client_error",
                    FlushedEvent => 'on_client_flush',
                    Filter       => POE::Filter::Line->new,
                );
                $_[HEAP]->{client_addr} = $client_addr;
                $_[KERNEL]->sig( SIG_Exit => 'cmd_exit' ); #only current session has registed this signal handler
            },
            _default => sub {
                my ($kernel, $event, $args) = @_[KERNEL, ARG0, ARG1];
                my $log = get_logger();
                unless( $event =~ /^_/ ) {
                    $kernel->post( $self->{alias}, $event, @$args ) unless $event =~ /^_/; #ignore internal event
                    $log->debug( "Unknown event '$event', forward it to session '$self->{alias}' for it is not internal event!" );
                }
            },
        },
        object_states => [
            $self => [ qw/on_client_command on_client_command_done on_client_error on_client_flush/ ],
        ],
    );
}

sub on_client_command {
    my ( $self, $heap, $kernel, $sess, $cmd ) = @_[OBJECT, HEAP, KERNEL, SESSION, ARG0];
    my $log = get_logger();
    my $sess_id = $sess->ID;
    $log->debug( "Client '$heap->{client_addr}'(session $sess_id) is ready to run command: $cmd!" );
    my $cmd_ref;
    eval {
        $cmd_ref = decode_json($cmd);
    };
    if( $@ ) {
        $cmd = encode_json{command => $cmd}; #temporarily use, only for manually test
    }
    $kernel->post(thread_pool => run => ($sess->ID, $cmd));
}

sub on_client_command_done {
    my ( $self, $heap, $result ) = @_[OBJECT, HEAP, ARG0];
    $heap->{rw}->put( $result );
    $heap->{ShutDown} = 1;
}

sub on_client_error {
    my ( $self, $kernel, $heap ) = @_[OBJECT, KERNEL, HEAP];
    my $log = get_logger();
    $log->error( "Client '$heap->{client_addr}' rw error: cause disconnected!" );
    $kernel->alarm_remove_all();
    delete $heap->{rw};
}

sub on_client_flush {
    my ( $self, $kernel, $heap ) = @_[OBJECT, KERNEL, HEAP];
    if( $heap->{ShutDown} ) {
        my $log = get_logger();
        $log->debug( "rw confirmed client '$heap->{client_addr}' need to be shutdown, release all resource!" );
        $kernel->alarm_remove_all();
        delete( $heap->{rw} );
    }
}

sub cmd_exit {
    my ( $self, $heap ) = @_[OBJECT, HEAP];
    my $log = get_logger();
    $log->debug( "cmd_tree emit cmd_exit event!" );
    $heap->{rw}->put( "Bye bye!" );
    $heap->{ShutDown} = 1;
}


### POE object state END ###

1;
