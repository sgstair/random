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

void led_set_red(int value)
{
	GPIO1DIR |= (1<<10);
	GPIO1DATA[(1<<10)] = value?(1<<10):0;
}


void led_set_green(int value)
{
	GPIO1DIR |= (1<<11);
	GPIO1DATA[(1<<11)] = value?(1<<11):0;
}


int heartbeatvalue;
// heartbeat: enable data transmission for the next 5 seconds.
void usb_heartbeat()
{
	// Synchronize with interrupt.
	InterruptDisable(INT_CT32B1);
	heartbeatvalue = 500;
	InterruptEnable(INT_CT32B1);
	led_set_red(1);
}


// 0 = all off, 1..4 = turn on one LED
void set_output_led(int led)
{
	GPIO1DIR |= 0x1E;
	GPIO1DATA[0x1E] = 0;
	
	if(led >= 1 && led <= 4)
	{
		GPIO1DATA[0x1E] = 1<<led;
	}
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
	//TMR32B1MR0 = 3000000; // Reset every 3M cycles (tick rate of 125ms @ 24MHz)
	TMR32B1MR0 = 240000; // Reset every 240k cycles (tick rate of 100hz/10ms @ 24MHz)

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
	
	led_set_red(0);
	
	// While in the timer interrupt, run the cycle to collect the data from our sensor and send the bytes if possible.
	// This will take some time but currently it doesn't really matter if everything else is blocked.
	
	int values[5];
	int adcr_base;
	int ad_out;
	
	for(int i=0;i<5;i++)
	{
		set_output_led(i);
		delayus(100);
		adcr_base = 0x2F01; // Select AD0 pin, 0x2F clock divider (effective AD clock is 0.5MHz. could run faster)
		
		values[i] = 0;
		// Run 16 conversions with 10 bits of accuracy, to get a 14-bit output.
		for(int n=0;n<16;n++)
		{
			AD0CR = adcr_base | 0x01000000; // Start conversion.
			ad_out = 0;
			while(!(ad_out&0x80000000))
			{
				ad_out = AD0GDR;
			}
			values[i] += (ad_out>>6)&0x3FF;
		}
	}
	set_output_led(0);
	
	// Send the values on their way to the host, if we can.
	if(Serial_BytesCanSend() >= 16 && heartbeatvalue > 0)
	{
		Serial_SendByte(0xFF);
		Serial_SendByte(0xFF);
		Serial_SendByte(timer_tick&255);
		Serial_SendByte((timer_tick>>8)&255);
		Serial_SendByte((timer_tick>>16)&255);
		Serial_SendByte((timer_tick>>24)&255);
		for(int i=0;i<5;i++)
		{
			Serial_SendByte(values[i]&255);
			Serial_SendByte((values[i]>>8)&255);
		}
		Serial_HintMoreData();
	}
	
	if(heartbeatvalue > 0)	
	{
		heartbeatvalue--;
	}
}






//---------------------------------------------------------------------------------
// Program entry point
//---------------------------------------------------------------------------------
int main(void) {
//---------------------------------------------------------------------------------

	heartbeatvalue = 0;


	SYSAHBCLKCTRL = 0x1655F; // Turn on clock to important devices (gpio, iocon, CT16B1, CT32B1, ADC)
	IOCON_PIO1_1 = 1 | IOCON_ADMODE_DIGITAL; // Default GPIO (led)

	IOCON_PIO0_6 = 1 ; // USB_CONNECT behavior.
	IOCON_PIO0_3 = 1 ; // USB_VBUS behavior.
	
	IOCON_PIO1_10 = 0 | IOCON_ADMODE_DIGITAL; // Red LED
	IOCON_PIO1_11 = 0; // Green LED
	
	IOCON_PIO0_11 = 2; // AD0, phototransistor input
	
	IOCON_PIO1_1 = 1 | IOCON_ADMODE_DIGITAL; // LED1
	IOCON_PIO1_2 = 1 | IOCON_ADMODE_DIGITAL; // LED2
	IOCON_PIO1_3 = 1 | IOCON_ADMODE_DIGITAL; // LED3
	IOCON_PIO1_4 = 0 | IOCON_ADMODE_DIGITAL; // LED4


	

	ReadDeviceUID();

	// Set up clocks for USB.
	if((MAINCLKSEL&3) == 0) // Assuming we are running on the RC osc..
	{
		// Wake up the Crystal OSC
		SYSOSCCTRL = 0;
		PDRUNCFG = 0x040 | 0x400; // Turn on SYSOSC, SYSPLL, USBPLL, ADC (not usb yet)
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


