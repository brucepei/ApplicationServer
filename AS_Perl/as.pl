#!/usr/bin/env perl
use lib 'lib';
use strict;
use warnings;
use POE;
use POE::Session;
use POE::Kernel;
use POE::Wheel;
use POE::Loop::Select;
use POE::Resource::Aliases;
use POE::Resource::Events;
use POE::Resource::Extrefs;
use POE::Resource::FileHandles;
use POE::Resource::SIDs;
use POE::Resource::Sessions;
use POE::Resource::Signals;
use POE::Wheel::SocketFactory;
use POE::Wheel::ReadWrite;
use POE::Wheel::Run;
use POE::Driver::SysRW;
use POE::Filter::Line;
use POE::Pipe::OneWay;
use POE::Pipe::TwoWay;
use POE::Component::Pool::Thread;
use POE::Component::AS;

Logger->new(path=>'log/as.log', append => 0);
POE::Session->create(
    inline_states => {
        _start => sub {
            POE::Component::AS->create(
                alias  => 'as',
                config => 'as.xml',
            );
            $_[KERNEL]->yield( 'start_as' );
            #$_[KERNEL]->delay( stop_shell  => 10 );
            #$_[KERNEL]->delay( start_shell => 20 );
        },
        start_as => sub {
            $_[KERNEL]->post( 'as', 'start' );
        },
        stop_as => sub {
            $_[KERNEL]->post( 'as', 'stop' );
        },
    },
);

POE::Kernel->run();
exit;