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
use IO::Pipely qw(pipely);

Logger->new(path=>'log/as.log', append => 0);

sub get_logger {
    my $package = shift || __PACKAGE__;
    Logger->is_init ? Logger->new : (die "Please init Logger module before use $package!");
}


my ($pipe_in, $pipe_out) = pipely(debug => 1);
$pipe_out->print( ": alive" );
$pipe_out->flush;
sysread($pipe_in, my $buffer = '', 7);
warn "input: [$buffer]";
<STDIN>;