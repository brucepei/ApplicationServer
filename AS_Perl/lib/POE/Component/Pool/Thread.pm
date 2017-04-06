package POE::Component::Pool::Thread;
# ----------------------------------------------------------------------------- 
# "THE BEER-WARE LICENSE" (Revision 43) borrowed from FreeBSD's jail.c: 
# <tag@cpan.org> wrote this file.  As long as you retain this notice you 
# can do whatever you want with this stuff. If we meet some day, and you think 
# this stuff is worth it, you can buy me a beer in return.   Scott S. McCoy 
# ----------------------------------------------------------------------------- 

use strict;
use warnings FATAL => "all";
 no warnings 'numeric'; # grep int hack
use threads;
use threads::shared;
use Thread::Semaphore;
use Thread::Queue;
use IO::Handle;
use POE qw( Pipe::OneWay Filter::Line Wheel::ReadWrite );
use IO::Pipely qw(pipely);
use Fcntl;

# Circumvent warnings...
BEGIN { run POE::Kernel }

*VERSION = \0.018; #lpei: 0.016: fix a bug based on 0.015: poe kernel stop in thread
                   #lpei: 0.017: fix a bug: pipe timeout 120s: hello alive
                   #lpei: 0.018: fix a bug: pipe timeout 120s: if no thread to detect alive, reload pipe once failed

use constant DEBUG => 1;
use constant ALIVE_PIPE_INTERVAL => 60;

sub new {
    die __PACKAGE__, "->new() requires a balanced list" unless @_ % 2;

    my ($type, %opt) = @_;
    
    $opt{inline_states} ||= {};
    $opt{StartThreads}  ||= 0;
    $opt{MinFree}       ||= 2;
    $opt{MaxFree}       ||= 10; 

    POE::Session->create    
    ( inline_states => {
        %{ $opt{inline_states} },

        _start => sub {
            my ($kernel, $heap) = @_[ KERNEL, HEAP ];

            $kernel->alias_set($opt{Name}) if $opt{Name};

            $heap->{queue} = [];

            # my ($pipe_in, $pipe_out) = POE::Pipe::OneWay->new;
            my ($pipe_in, $pipe_out) = pipely(debug => 1); #lpei: use IO::Pipely, since POE::Pipe has been deprecated
            $heap->{pipe_out} = $pipe_out;
            $kernel->delay(-alive_pipe => ALIVE_PIPE_INTERVAL);
            
            die "Unable to create pipe" 
            unless defined $pipe_in and defined $pipe_out;

            $heap->{wheel} = POE::Wheel::ReadWrite->new
                ( Handle      => $pipe_in,
                  InputEvent  => "-thread_talkback",
                  ErrorEvent  => "-thread_talkerror",
                );

            for (1 .. $opt{StartThreads}) {
                $kernel->call($_[SESSION], "-spawn_thread");
            }

            goto $opt{inline_states}{_start} if $opt{inline_states}{_start};
        },
        
        #lpei: fix bug: pipe_out forcily close after 120 seconds: hello alive
        -alive_pipe => sub {
            my ($kernel, $heap) = @_[ KERNEL, HEAP ];
            my $find_free_thread = 0;
            my $force_tid;
            for my $tid (keys %{ $heap->{thread} }) {
                $force_tid = $tid;
                if ( ${ $heap->{thread}{$tid}{semaphore} } ) {
                    $heap->{thread}{$tid}{iqueue}->enqueue("alive");
                    DEBUG && warn "start to detect pipe alive at thread $tid...";
                    $find_free_thread = 1;
                    last;
                }
            }
            if (!$find_free_thread && defined($force_tid)) {
                $heap->{thread}{$force_tid}{iqueue}->enqueue("alive");
                DEBUG && warn "start to detect pipe alive at force thread $force_tid...";
            }
            $kernel->delay(-alive_pipe => ALIVE_PIPE_INTERVAL);
        },
        
        #lpei: fix bug: pipe_out forcily close after 120 seconds, reload pipe
         -reload_pipe => sub {
            my ($kernel, $session, $heap) = @_[ KERNEL, SESSION, HEAP ];
            my @time = localtime(time);
            DEBUG && warn "[$time[2]:$time[1]:$time[0]] PIPE failed: 1. joining all threads";
            for my $tid (keys %{ $heap->{thread} }) {
                my $tdsc = $heap->{thread}{$tid};
                $tdsc->{iqueue}->enqueue("last");
                $tdsc->{thread}->join;
                DEBUG && warn "PIPE failed: 1.1 thread $tid joined";
                unless ($kernel->refcount_decrement($session->ID, "thread")) {
                    DEBUG && warn "PIPE failed: 2. delete pipe wheel";
                    delete $heap->{wheel};
                }
                delete $tdsc->{$_} for keys %$tdsc;
            }
            $heap->{thread} = {};
            DEBUG && warn "PIPE failed: 3. create new pipe";
            my ($pipe_in, $pipe_out) = pipely(debug => 1);
            unless( defined $pipe_in and defined $pipe_out ) {
                DEBUG && warn "Unable to recreate pipe, and retry later!";
                $kernel->delay('-reload_pipe' => 1);
                return;
            }
            $heap->{pipe_out} = $pipe_out;
            DEBUG && warn "PIPE failed: 4. create new pipe wheel";
            $heap->{wheel} = POE::Wheel::ReadWrite->new
                ( Handle      => $pipe_in,
                  InputEvent  => "-thread_talkback",
                  ErrorEvent  => "-thread_talkerror",
                );
            @time = localtime(time);
            DEBUG && warn "[$time[2]:$time[1]:$time[0]] PIPE failed: 5. create new prespawn threads";
            for (1 .. $opt{StartThreads}) {
                $kernel->call($_[SESSION], "-spawn_thread");
            }
        },
        
        _stop => sub {
            my ($kernel, $heap) = @_[ KERNEL, HEAP ];

            DEBUG && warn "Joining all threads";
            for my $tid (keys %{ $heap->{thread} }) {
                $heap->{thread}{$tid}{iqueue}->enqueue("last");
                $heap->{thread}{$tid}{thread}->join;
            }

            goto $opt{inline_states}{_stop} if $opt{inline_states}{_stop};
        },

        _default => sub {
            die "_default caught state: ", $_[ARG0];
        },

        -thread_talkerror => sub { 
            my ($kernel, $heap) = @_[ KERNEL, HEAP ];
            warn "PIPE failed: type=", $_[ARG0], ", msg=", $_[ARG2];
            $kernel->yield('-reload_pipe');
        },

        -thread_talkback => sub {
            my ($kernel, $heap, $input) = @_[ KERNEL, HEAP, ARG0 ];
            my ($tid, $command) = ($input =~ m/(\d+): (\w+)/);

            DEBUG and warn "Recieved: $input";

            # Depending upon the settings of perlvar's, its possible we may get
            # some garbage through here.
            if (defined $command) {
                if ($command eq "cleanup") {
                    $kernel->yield(-execute_cleanup => $tid);
                }
                elsif ($command eq "collect") {
                    $kernel->yield(-collect_garbage => $tid);
                }
                elsif ($command eq "alive") {#lpei: fix bug: pipe_out forcily close after 120 seconds
                    DEBUG && warn "confirm pipe alive!";
                    $kernel->delay(-alive_pipe => ALIVE_PIPE_INTERVAL); #lpei: reset alive timer, to avoid alive so quickly
                }
            }
        },

        -collect_garbage => sub {
            DEBUG && warn "GC Called, thread exited";
            
            my ($kernel, $session, $heap, $tid) = 
                @_[ KERNEL, SESSION, HEAP, ARG0 ];

            my $tdsc = delete $heap->{thread}{$tid} or return;

            $tdsc->{thread}->join;

            unless ($kernel->refcount_decrement($session->ID, "thread")) {
                delete $heap->{wheel};
            }

            delete $tdsc->{$_} for keys %$tdsc;
        },

        -execute_cleanup => sub {
            my ($kernel, $session, $heap, $tid) = 
                @_[ KERNEL, SESSION, HEAP, ARG0 ];

            DEBUG && warn "GC Called, thread finished task";

            my $thread = $heap->{thread};
            my @free   = grep ${ $_->{semaphore} }, values %$thread;

            my $queue  = $heap->{queue};
            my $rqueue = $heap->{thread}{$tid}{rqueue};
            my $iqueue = $heap->{thread}{$tid}{iqueue};

            if ($rqueue->pending) {
                if ($opt{CallBack}) {
                    DEBUG && warn "Dispatching CallBack";
                    $opt{CallBack}->( @_[0..ARG0-1], @{$rqueue->dequeue} );
                }
            }

            if (@$queue) {
                my $args = &share([]);
                push @$args, @{ shift @$queue };

                $iqueue->enqueue($args);
            }
            elsif (@free > $opt{MaxFree}) {
                (shift @free)->{iqueue}->enqueue("last");
            }
            $kernel->delay(-alive_pipe => ALIVE_PIPE_INTERVAL); #lpei: reset alive timer, to avoid alive so quickly
        },

        -spawn_thread => sub {
            my ($kernel, $session, $heap) = @_[ KERNEL, SESSION, HEAP ];
            
            return if $opt{MaxThreads} == scalar keys %{ $heap->{thread} };
            DEBUG && warn "Spawning a new thread";

            my $semaphore   = Thread::Semaphore->new;
            my $iqueue      = Thread::Queue->new;
            my $rqueue      = Thread::Queue->new;
            my $pipe_out    = $heap->{pipe_out};
            my $queue       = $heap->{queue};

            my $thread      = threads->create
                ( \&thread_entry_point, 
                  $semaphore, 
                  $iqueue, 
                  $rqueue, 
                  fileno($pipe_out),
                  $opt{EntryPoint} );

            $kernel->refcount_increment($session->ID, "thread");

            $heap->{thread}{$thread->tid} = { 
                semaphore   => $semaphore,
                iqueue      => $iqueue,
                rqueue      => $rqueue,
                thread      => $thread,
                lifespan    => 0, # Not currently used
            };

            if (@$queue) {
                my $args = &share([]);
                push @$args, @{ shift @$queue };

                $iqueue->enqueue($args);
            }
        },
        
        run => sub {
            my ($kernel, $heap, @arg) = @_[ KERNEL, HEAP, ARG0 .. $#_ ];

            DEBUG && warn "Assigned a task";

            my $thread = $heap->{thread};
            my @free   = grep ${ $_->{semaphore} }, values %$thread;

            if (@free) {
                my $tdsc = shift @free;

                # Trickery so we can pass this through Thread::Queue;
                my $sharg = &share([]);

                # Just to be polite...
                lock $sharg;
                push @$sharg, @arg;

                DEBUG and warn "Enqueueing on ", $tdsc->{thread}->tid;

                $tdsc->{iqueue}->enqueue($sharg);
            }
            else {
                push @{ $heap->{queue} }, [ @arg ];
            }

            if (@free < $opt{MinFree}) {
                unless (scalar(keys %$thread) >= $opt{MaxThreads}) {
                    $kernel->yield("-spawn_thread");
                }
            }
        },

        shutdown => sub {
            my ($kernel, $heap) = @_[ KERNEL, HEAP ];

            $heap->{shutdown} = 1;
            $kernel->alias_remove($opt{Name});

            for my $thread (values %{ $heap->{thread} }) {
                $thread->{iqueue}->enqueue("last");
            }
        },
      },
    );
}

sub thread_entry_point {
    my ($semaphore, $iqueue, $rqueue, $pipe_fd, $task) = @_;
    $poe_kernel->stop(); #lpei fixed: need to stop POE in child thread, or it will impact event loop
    my $pipe = IO::Handle->new_from_fd($pipe_fd, "a") or die $!;

    # XXX Hack
    my $code = $task;

    # Just incase
    local $\ = "\n";

    while (my $action = $iqueue->dequeue) {
        DEBUG and warn threads->self->tid, ": received action";
        $semaphore->down;

#       lock $action;

        unless (ref $action) {
            if ($action eq "last") {
                $$semaphore = -1;
                last;
            }
            elsif ($action eq "alive") { #lpei: fix bug: pipe_out forcily close after 120 seconds
                DEBUG and warn threads->self->tid, ": detect pipe alive?";
                $pipe->print( threads->self->tid, ": alive" );
                $pipe->flush;
            }
        }

        else { 
            my $arg = $action;
#           lock $arg;

            # Just incase...
            my $result = &share([]);
            push @$result, $code->(@$arg);

            DEBUG and warn threads->self->tid, ": Enqueuing result: @$result";
            $rqueue->enqueue($result);
        }

        DEBUG and warn threads->self->tid, ": Requesting cleanup";

        $pipe->print( threads->self->tid, ": cleanup" );
        $pipe->flush;

        $semaphore->up;
    }
    # DEBUG && warn threads->self->tid . "child pipe:". fileno($pipe);
    $pipe->print( threads->self->tid, ": collect" );
    $pipe->flush;
    DEBUG and warn threads->self->tid, ": Requesting Destruction";
}

1;