/*

hrv_monitor Microcontroller project

Copyright (c) 2014 Stephen Stair (sgstair@akkit.org)

Permission is hereby granted, free of charge, to any person obtaining a copy
 of this software and associated documentation files (the "Software"), to deal
 in the Software without restriction, including without limitation the rights
 to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
 all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 THE SOFTWARE.
*/

typedef unsigned char u8;
typedef unsigned long u32;

#include "lpc13xx.h"
#include "system.h"
#include "dpc.h"
#include "winusbserial.h"
#include "fifobuf.h"







////////////////////////////////////////////////////////////////////////////////
//
//  Stateless I/O
//

void led_set(int value)
{
	GPIO1DIR |= 0x2;
	GPIO1DATA[2] = value?2:0;
}




////////////////////////////////////////////////////////////////////////////////
//
//  System / Timing
//

volatile unsigned int timer_tick;

void timer_init()
{
	InterruptDisable(INT_CT32B1);
	TMR32B1TCR = 0; // disable
	InterruptClear(INT_CT32B1);
	InterruptEnable(INT_CT32B1);
	TMR32B1PR = 0; 
	TMR32B1PC = 0; // reset values
	TMR32B1TC = 0;

	TMR32B1IR = TMR32B1IR; // Reset interrupt flags.

	// Interrupt and reset on match register 0
	TMR32B1MCR = 3;
	//TMR32B1MR0 = 1500000; // Reset every 1.5M cycles (tick rate of 125ms @ 12MHz)
	TMR32B1MR0 = 3000000; // Reset every 3M cycles (tick rate of 125ms @ 24MHz)

	timer_tick = 0;

	TMR32B1TCR = 1; // Enable
}

unsigned int timer_get_tick()
{
	return timer_tick;
}
unsigned int timer_wait_tick()
{
	u32 tick = timer_get_tick();
	while(tick == timer_get_tick()) asm("WFI"); // lower power but MAY skip a tick in very rare circumstances. Unlikely in this code.
	return timer_get_tick();
}

extern "C" void int_CT32B1();
void int_CT32B1()
{
	// Clear pending interrupt
	TMR32B1IR = TMR32B1IR;
	InterruptClear(INT_CT32B1);


	timer_tick++;
}






//---------------------------------------------------------------------------------
// Program entry point
//---------------------------------------------------------------------------------
int main(void) {
//---------------------------------------------------------------------------------


	SYSAHBCLKCTRL = 0x1655F; // Turn on clock to important devices (gpio, iocon, CT16B1, CT32B1, ADC)
	IOCON_PIO1_1 = 1 | IOCON_ADMODE_DIGITAL; // Default GPIO (led)

	IOCON_PIO0_6 = 1 ; // USB_CONNECT behavior.
	IOCON_PIO0_3 = 1 ; // USB_VBUS behavior.
	
	IOCON_PIO0_8 = 0;
	IOCON_PIO0_9 = 0;

	

	ReadDeviceUID();

	// Set up clocks for USB.
	if((MAINCLKSEL&3) == 0) // Assuming we are running on the RC osc..
	{
		// Wake up the Crystal OSC
		SYSOSCCTRL = 0;
		PDRUNCFG = 0x050 | 0x400; // Turn on SYSOSC, SYSPLL, USBPLL (not usb yet)
		delayms(5); // Give clock some time to warm up
		// Setup SYSPLL to provide 24MHz cpu CLK. M=2, P=4
		// Note that 24MHz is technically out of spec (should set waitstates for flash at >20MHz), but this works and is simpler.
		SYSPLLCTRL = 0x41;
		SYSPLLCLKSEL = 1; // select OSC
		SYSPLLCLKUEN=0;
		SYSPLLCLKUEN=1; // update clock source 
		// Setup USBPLL to provide 48MHz USB CLK. M=4, P=2
		USBPLLCTRL = 0x23;
		USBPLLCLKSEL = 1;
		USBPLLCLKUEN=0;
		USBPLLCLKUEN=1; // Update clock source

		// Wait for PLLs to stabilize
		while((SYSPLLSTAT&1) == 0);
		while((USBPLLSTAT&1) == 0);
		delayms(100);
		// Switch system clock over to PLL clock
		MAINCLKSEL = 3;
		MAINCLKUEN = 0;
		MAINCLKUEN = 1;
	}

	delayms(10);

	// Setup periodic timer at 125ms intervals.
	timer_init();

	dpc_init();
	dpc_suspend();


	PDRUNCFG &= ~0x400; // Turn on USB
	usb_init();

	dpc_resume();

	// Don't return.
	while(1)
	{
		timer_wait_tick();

	}
}


