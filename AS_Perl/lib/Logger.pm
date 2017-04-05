package Logger;
use strict;
use warnings;
use Carp 'croak';
use Fcntl ':flock';
use File::Path 'mkpath';
use ObjectPeil;
use threads;

our @ISA = qw( ObjectPeil );
attribute(qw( path level die_log append ));
$SIG{__DIE__} = \&handle_die;
our $Called_Depth = 5; #The caller func, support this package will be wrapped again, so use '5', or it should use '4'
my $Crash_log_file = 'crash_dump.log'; #default file for 'die_log'

my $singleton;
sub new {
    return $singleton if $singleton;
    $singleton = shift->SUPER::new(
        history => [],
        level => 'debug', #default level to log
        max_history_size => 10,
        path => undef,
        die_log => $Crash_log_file,
        append => 1,
        @_,
    );
    my $log_dir = $singleton->{path};
    $log_dir =~ s/(?<=[\\\/])[^\\\/]+$//;
    mkpath($log_dir) unless -d $log_dir;
    unlink $singleton->{path} if !$singleton->{append} && (-e $singleton->{path});
    $singleton;
}

# Supported log level
my $LEVEL = {trace => 0, debug => 1, info => 2, warn => 3, error => 4, fatal => 5};

sub is_init {
    return $singleton ? 1 : 0;
}

sub handle {
    if (my $path = shift->{path}) {
        croak qq{Can't open log file "$path": $!}
            unless open my $file, '>>', $path;
        return $file;
    }
    return \*STDERR;
}

sub append {
    my ($self, $msg) = @_;
    return unless my $handle = $self->handle;
    
    flock $handle, LOCK_EX;
    $handle->print($msg) or croak "Can't write to log: $!";
    flock $handle, LOCK_UN;
}

sub trace { shift->log(trace => @_) }
sub debug { shift->log(debug => @_) }
sub info  { shift->log(info  => @_) }
sub warn { shift->log(warn => @_) }
sub error { shift->log(error => @_) }
sub fatal { shift->log(fatal => @_) }

sub log { shift->_message(lc shift, @_) }

sub is_level {
    $LEVEL->{lc pop} >= $LEVEL->{shift->{level}};
}


sub _format {
    my @time = localtime(shift);
    my @caller_info = caller($Called_Depth); #caller function: main()
    my @called_info = caller($Called_Depth - 1); #The called ('debug/warn/...') func at xxx line
    "[$time[2]:$time[1]:$time[0]] [" . shift() . "] [$caller_info[3] $called_info[2]] " . join("\n", @_, '');
}

sub _message {
    my ($self, $level) = (shift, shift);
    return unless $self->is_level($level); #should be greater than default level
    
    my $max     = $self->{max_history_size};
    my $history = $self->{history};
    push @$history, my $msg = [time, $level, @_];
    shift @$history while @$history > $max;
    my $log_msg = _format(@$msg);
    print $log_msg;
    $self->append($log_msg);
}


sub handle_die {
    # return if $^S; #if in an eval {}, don't want actually die
    # $^S && die @_;
    die @_ if (!defined($^S) or $^S);
    my $mess = join('', @_);
    my @died_call = caller(0);
    $mess =~ s/\n$//;
    $mess = "Oops, died!\n -> $died_call[1], die at line $died_call[2], msg: '$mess'\n";
    
    my $i = 1;
    local $" = ", ";
    {
        package DB;
        while (my @parts = caller($i++)) {
            my $param = '';
            if( @DB::args ) {
                my @p = map { ref($_) eq 'ARRAY' ? '[' . join(', ', @$_) . ']' :
                              ref($_) eq 'HASH' ? '{' . join(', ', %$_) . '}' :
                              defined($_) ? $_ : 'undef' } @DB::args;
                $param = "@p";
            }
            $mess .= " -> $parts[1], $parts[3] $parts[2] ($param)\n";
        }
    }
    eval {
        if( $singleton && $singleton->is_init ) {
            #local $Called_Depth;
            $Called_Depth += 2; #eval block and handle_die, they add 2 levels
            $singleton->fatal($mess);
            $Called_Depth -= 2;
        }
        else {
            die "Crash without Logger instance!\n";
        }
    };
    if( $@ ) {
        chomp($@);
        my $crash_log = $singleton ? $singleton->{die_log} : $Crash_log_file;
        $mess = "%WARNING: $@ So log into '$crash_log'\n$mess";
        open( my $hf, ">", $crash_log ) or
            die "$mess\n\t####Can't create '$crash_log'!####";
        print $hf scalar localtime() . "\n";
        print $hf $mess . "\n";
        close $hf;
    }
    die $mess;
}

1;
