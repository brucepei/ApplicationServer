package ObjectPeil;

use strict;
use warnings;
# 'use Exporter import', take the same effect when 'require Exporter' with 'our @ISA = "Exporter"'
# The fore part, would import the function 'import' into current package, and the other would inherit the import from 'Exporter'
use Exporter 'import';
#
#require Exporter;
#our @ISA = qw( Exporter );
our @EXPORT = qw( attribute );
my $DEBUG = 0;

attribute();

sub attribute {
   my $pkg = caller;
   my $code = '';#avoid uninitialized value when use concatenation (.)
   {
      no strict 'refs';
      @{"${pkg}::_ATTRIBUTES_"} = @_;
   }
   #because we wanna inherit new() from super class, not build a new constructor
   print "Call attribute() from $pkg, but __PACKAGE__ is " . __PACKAGE__ . "\n" if $DEBUG >= 1;
   $code = _define_constructor( $pkg ) if $pkg eq __PACKAGE__; #only this package 'ObjectPeil' will have new method
   $code .= _define_accessor( $pkg, @_ ) || ''; #avoid uninitialized value when use concatenation (.)
   print $code if $DEBUG >= 1;
   eval $code;
   die( "Cannot define constructor! $@ !\n") if $@;
}

sub _define_constructor {
   my $pkg = shift;
   my $code;
   $code = qq(
      package $pkg;
      sub new {
         my \$class = shift;
         \$class = ref( \$class ) || \$class;
         if (\@_ % 2) {
            die "Odd number attributes!\\n";
         }
         my \%attrib = \@_;
         bless \\\%attrib, \$class;
      }
   );
   $code;
}

sub _define_accessor {
   my $pkg = shift;
   my $code;
   my $result = get_attribute_names( $pkg ); #it will get attrs of all parents' and current class's attrs, so parent and child will have different attr accessor
   foreach (@$result) {
      unless ( $pkg->can( $_ )) {
         $code .= qq(
            package $pkg;
            sub $_ {
               my \$obj = shift;
               \@_ > 0 ? \$obj->{ $_ } = \$_[0] : \$obj->{ $_ };
            }
         );
      }
   }
   $code;
}

sub get_attribute_name_value {
   my ($pkg) = @_;
   my @name_list = $pkg->get_attribute_names;
   print "Get name list: @name_list\n" if $DEBUG >= 10;
   my @name_value_list;
   foreach ( @name_list ) {
      push( @name_value_list, $_, $pkg->$_ ); #in fact, all attrs even inherid from parent, will also have a copy in local hash
   }
   wantarray ? @name_value_list : \@name_value_list;
}

#get attrs of all parents' and current class's attrs -- No duplicated attrs
sub get_attribute_names {
   my ($pkg) = @_;
   $pkg = ref( $pkg ) || $pkg;
   {
      no strict 'refs';
      my @result;
      if ( ${pkg}->isa( __PACKAGE__) ) {
         if ( @{"${pkg}::ISA"} ) {
            print "$pkg has \@ISA\n" if $DEBUG  >= 5;
            foreach (@{"${pkg}::ISA"}) {
               if ( $_->isa( __PACKAGE__ ) ) {
                  print "$_ is supper class of " . __PACKAGE__ . "\n" if $DEBUG >= 5;
                  push( @result, get_attribute_names($_) );#here, all parents' attrs would be save into @result
               }
            }
         }
         my @not_existed_attr;
         foreach my $try_attr ( @{"${pkg}::_ATTRIBUTES_"} ) {
            my $is_existed;
            foreach my $existed_attr ( @result ) { #travel all parents' attrs, if parent already has attr, then don't count the attr in sub class
               if ($try_attr eq $existed_attr ) {
                  $is_existed = 1;
                  last;
               }
            }
            next if $is_existed;
            push(@not_existed_attr, $try_attr);
         }
         push(@result, @not_existed_attr);
         print "capture $pkg 's attributes: @result\n" if $DEBUG >= 5;
      }
      wantarray ? @result : \@result;
   }
}

sub equals {
   my ($obj, $other) = @_;
   return $obj == $other ? 1 : 0;
}

sub clone {
   my $obj = shift;
   $obj->new( $obj->get_attribute_name_value );
}

sub recursive_clone {
   my $obj = shift;
   if ( UNIVERSAL::isa( $obj, __PACKAGE__ ) ) {
      my @names = $obj->get_attribute_names;
      my @name_values;
      foreach (@names) {
         my $value = $obj->$_;
         if ( UNIVERSAL::isa( $value, __PACKAGE__ ) ) {
            push( @name_values, $_, $value->recursive_clone );
         }elsif ( ref($value) eq 'ARRAY' ) {
            push( @name_values, $_, recursive_clone( $value ) );
         }elsif ( ref($value) eq 'HASH' ) {
            push( @name_values, $_, recursive_clone( $value ) );
         }else {
            push( @name_values, $_, $value );
         }
      }
      $obj->new( @name_values );
   }elsif (ref($obj) eq 'ARRAY') {
      my @clone_array = ();
      foreach my $value ( @$obj ) {
         if ( UNIVERSAL::isa( $value, __PACKAGE__ ) ) {
            push( @clone_array, $value->recursive_clone );
         }elsif ( ref($value) eq 'ARRAY' ) {
            push( @clone_array, recursive_clone( $value ) );
         }elsif ( ref($value) eq 'HASH' ) {
            push( @clone_array, recursive_clone( $value ) );
         }else {
            push( @clone_array, $value );
         }
      }
      \@clone_array;
   }elsif (ref($obj) eq 'HASH') {
      my %clone_hash = ();
      foreach ( keys %$obj ) {
         my $value = $obj->{$_};
         if ( UNIVERSAL::isa( $value, __PACKAGE__ ) ) {
            $clone_hash{$_} = $value->recursive_clone;
         }elsif ( ref($value) eq 'ARRAY' ) {
            $clone_hash{$_} = recursive_clone( $value );
         }elsif ( ref($value) eq 'HASH' ) {
            $clone_hash{$_} = recursive_clone( $value );
         }else {
            $clone_hash{$_} = $value;
         }
      }
      \%clone_hash;
   }
}


1;
