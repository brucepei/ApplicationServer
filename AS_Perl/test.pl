#!/usr/bin/env perl
use lib 'lib';
use strict;
use warnings;
use POE;
use POE::Session;
use POE::Kernel;
use POE::Wheel;
use POE::Component::Pool::Thread;
use Logger;
use JSON;

Logger->new(path=>'log/as.log', append => 0);

sub get_logger {
    my $package = shift || __PACKAGE__;
    Logger->is_init ? Logger->new : (die "Please init Logger module before use $package!");
}


sub run_received_command {
    my ($sender, $args_json) = @_;
    my $log = get_logger();
    my ($args, $json_result);
    if( 1 ) {
        $log->debug("Run command: '$args_json'!");
        $json_result = encode_json( {stdout => undef, stderr => $@, exit => -1} );
    } else {
        $log->debug("Begin to run command: $args->{command} from session $sender!");
        $json_result = encode_json( {stdout => undef, stderr => undef, exit => 0} );
        $log->debug("End to run command: $args->{command} from session $sender!");
    }
    return ($sender, $json_result);
}


sub on_complete_command {
    my ($kernel, $sender, $json_result) = @_[ KERNEL, ARG0, ARG1 ];
    my $log = get_logger();
    $log->debug("Cet command result for session $sender: '$json_result'!");
}

POE::Session->create(
    inline_states => {
        _start => sub {
            $_[HEAP]->{thread_pool} = POE::Component::Pool::Thread->new(
                MinFree       => 2,
                MaxFree       => 5,
                MaxThreads    => 15,
                StartThreads  => 2,
                Name          => "thread_pool",
                EntryPoint    => \&run_received_command,
                CallBack      => \&on_complete_command,
            );
            $_[KERNEL]->delay(alive => 1, 0)
        },
        alive => sub {
            my $counter = $_[ARG0];
            $counter++;
            print "hello $counter\n";
            # if ($counter < 10 || $counter > 140) {
                # $_[KERNEL]->post(thread_pool => run => $_[SESSION]->ID, $counter);
            # }
            # else {
                # warn "counter remains: " . $counter;
            # }
            $_[KERNEL]->delay( alive => 1, $counter );
        },
    },
);

POE::Kernel->run();
exit;