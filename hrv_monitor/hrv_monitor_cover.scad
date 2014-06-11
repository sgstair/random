
width=1;

gap_size = 6.8;
gap_width = 1.7;

nongap = 8.2;
nonwid = 10;

latchsize = 0.4;

disc_dia = 24;
disc_hei = 0.5;

board_hei = 2.5;
further_hei = 1.5;

total_hei = disc_hei + board_hei + further_hei;


module arm()
{
	translate([-nonwid/2,nongap/2,0])
	cube([nonwid, width, total_hei]);

	translate([-gap_width/2, gap_size/2,0])
	cube([gap_width,width+(nongap-gap_size)/2,total_hei]);

	translate([-gap_width/2, gap_size/2-latchsize,disc_hei+board_hei])
	cube([gap_width,latchsize+width+(nongap-gap_size)/2,further_hei]);

}

union()
{
	cylinder(r=disc_dia/2, h=disc_hei);
	arm();
	rotate([0,0,180]) arm();	
}