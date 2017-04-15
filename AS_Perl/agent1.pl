use lib 'lib';
use warnings;
use strict;
use diagnostics;
use POE;
use POE::Filter::Stream;
use POE::Filter::Line;
use POE::Component::Proxy::TCP;
$|++;  
 
POE::Component::Proxy::TCP->new(
 Alias                      => "proxy_server",
 Port                       => 8080,
 OrigPort                   => 16789,
 OrigAddress                => "127.0.0.1",
 DataFromClient             => sub {print "From client:", shift(), "\n";},
 DataFromServer             => sub {print "From server:", shift(), "\n";},
 RemoteClientFilter         => "POE::Filter::Stream",
 RemoteServerOutputFilter   => "POE::Filter::Stream",
 RemoteServerInputFilter    => "POE::Filter::Stream"
);
 
$poe_kernel->run();
exit 0;