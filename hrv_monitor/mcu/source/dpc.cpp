/*
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

#include "lpc13xx.h"
#include "dpc.h"
#include "winusbserial.h"
#include "system.h"

unsigned char dpc_suspendcount;

void dpc_work()
{
	int shouldhint = 0;
	while(Serial_CanSendByte() && Serial_CanRecvByte())
	{
		shouldhint = 1;
		Serial_SendByte(Serial_RecvByte());
	}
	if(shouldhint) Serial_HintMoreData();
}

extern "C" void int_I2C0(); // use I2C0 for now, because it isn't being used by anything else.
void int_I2C0()
{
	usb_trace('D');
	InterruptClear(INT_I2C0);
	dpc_work();
}

void dpc_trigger()
{
	usb_trace('d');
	InterruptTrigger(INT_I2C0);
}

void dpc_suspend()
{
	InterruptDisable(INT_I2C0);
	dpc_suspendcount++;
}

void dpc_resume()
{
	dpc_suspendcount--;
	if(dpc_suspendcount==0)
		InterruptEnable(INT_I2C0);
}

void dpc_init()
{
	InterruptDisable(INT_I2C0);
	InterruptSetPriority(INT_I2C0,31);
	InterruptClear(INT_I2C0);
	dpc_suspendcount=0;

	InterruptEnable(INT_I2C0);
}