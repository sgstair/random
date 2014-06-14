using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using winusbdotnet;

namespace demo_app
{
    public class hrv_monitor_instance
    {
        internal hrv_monitor_instance(string devicePath)
        {
            DevicePath = devicePath;
        }

        public readonly string DevicePath;
    }

    public class hrv_monitor
    {
        /// <summary>
        /// Enumerate hrv_monitor devices in the system
        /// </summary>
        /// <returns>List of device instance classes</returns>
        public static hrv_monitor_instance[] EnumerateDevicePaths()
        {
            string[] devicePaths = WinUSBDevice.EnumerateDevices(new Guid("2bbdd6f9-37bd-4f86-8eba-0aa34476afde"));
            hrv_monitor_instance[] instances = new hrv_monitor_instance[devicePaths.Length];
            for(int i=0;i<devicePaths.Length;i++)
            {
                instances[i] = new hrv_monitor_instance(devicePaths[i]);
            }
            return instances;
        }

        public hrv_monitor_data Data;

        internal WinUSBDevice Device;

        public hrv_monitor(hrv_monitor_instance instance)
        {
            Device = new WinUSBDevice(instance.DevicePath);
            Data = new hrv_monitor_data(this);
        }

        public void Close()
        {
            Data.Close();
            Device.Close();
            Data = null;
            Device = null;
        }
    }

    public class hrv_monitor_data
    {
        public const double DataPeriodMs = 100;

        hrv_monitor Monitor;
        Timer DataTick;
        internal hrv_monitor_data(hrv_monitor parent)
        {
            Frozen = false;

            Monitor = parent;
            DataTick = new Timer(TimerTick);

            Monitor.Device.EnableBufferedRead(0x83);

            SpanData = new List<hrv_monitor_data_span>();

            // Start ticking.
            DataTick.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(DataPeriodMs));
        }

        /// <summary>
        /// Wind down the background stuff and stop relying on the device.
        /// </summary>
        internal void Close()
        {
            lock(this)
            {
                DataTick.Change(-1, -1); // Stop the timer.

                Monitor = null; // Unlink
            }
        }

        /// <summary>
        /// On a timer, read all data from 
        /// </summary>
        /// <param name="context">Not used</param>
        private void TimerTick(object context)
        {
            bool haveData = false;
            lock (this)
            {
                if (Monitor == null)
                {
                    return;
                }

                SendHeartbeat();


                // Read all buffered data from the pipe into the storage system.
                while(Monitor.Device.BufferedByteCountPipe(0x83) > 16)
                {
                    byte[] data = Monitor.Device.BufferedPeekPipe(0x83, 16);
                    if(hrv_monitor_sample.CheckData(data))
                    {
                        // Assume data is valid, read
                        Monitor.Device.BufferedSkipBytesPipe(0x83, 16);
                        hrv_monitor_sample sample = new hrv_monitor_sample(data);
                        QueueSample(ref sample);
                        haveData = true;
                    }
                    else
                    {
                        // Must be misaligned, skip to the next byte and try again.
                        Monitor.Device.BufferedSkipBytesPipe(0x83, 1);
                    }

                }
            }
            // If we have data, notify any subscribers.
        }

        private void SendHeartbeat()
        {
            // Assume we know Monitor is valid.
            // Send any byte, will cause the device to send updates for the next 5 seconds.
            Monitor.Device.WritePipe(0x03, new byte[] { 0x00 });
        }


        private void QueueSample(ref hrv_monitor_sample sample)
        {
            // Can we queue it in an existing last span?
            if(SpanData.Count > 0)
            {
                if(SpanData[SpanData.Count-1].NextSequence == sample.SequenceNumber)
                {
                    // All yours!
                    SpanData[SpanData.Count - 1].QueueSample(ref sample);
                    return;
                }
            }

            // no, make a new span and queue it there.


        }

        private bool Frozen;
        private List<hrv_monitor_data_span> SpanData;
        int FrozenSpanCount;



        /// <summary>
        /// Prevent the number of samples in the system from building up to a ridiculous number.
        /// This function will get rid of early data in the system.
        /// Must not be called when frozen. 
        /// It's not a good idea to interact with this object in other threads while trimming.
        /// </summary>
        /// <param name="samplesToKeep">Minimun number of samples to keep.</param>
        public void TrimEarlySamples(int samplesToKeep)
        {
            lock (this)
            {
                if (Frozen)
                {
                    throw new Exception("Must not TrimEarlySamples while frozen.");
                }

                // Count the samples we have.
                int total = TotalSamples;

                int canDelete = TotalSamples - samplesToKeep;
                if(canDelete <= 0) return; // Nothing to do.

                // Walk through the list of spans and completely eliminate spans that we can.
                int deleteIndex;
                for (deleteIndex = 0; deleteIndex < SpanData.Count; deleteIndex++)
                {
                    if (SpanData[deleteIndex].Count > canDelete)
                    {
                        break;
                    }
                    canDelete -= SpanData[deleteIndex].Count; // We will delete this span.
                }
                if(deleteIndex > 0)
                {
                    SpanData.RemoveRange(0, deleteIndex);
                }

                if(SpanData.Count > 0)
                {
                    // There's still at least one span. The first one is the next one to attack.
                    if(canDelete > 0)
                    {
                        // Request this span to trim itself.
                        SpanData[0].TrimEarlySamples(canDelete);
                    }
                }

            }
        }

        /// <summary>
        /// Freeze the virtual state of the data so rendering/etc operations can occur atomically
        /// Data continues to be collected in the background, but will not change the set of visible data
        /// </summary>
        public void Freeze()
        {
            lock(this)
            {
                if(Frozen == true)
                {
                    return;
                }

                foreach (hrv_monitor_data_span span in SpanData)
                {
                    span.Freeze();
                }

                FrozenSpanCount = SpanData.Count;
                Frozen = true;

            }

        }

        /// <summary>
        /// Enables live update after a freeze - When the data system is thawed, data from anoter thread
        /// can be added to the system at any time.
        /// </summary>
        public void Thaw()
        {
            lock (this)
            {
                Frozen = false;
                foreach (hrv_monitor_data_span span in SpanData)
                {
                    span.Thaw();
                }
            }
        }


        public hrv_monitor_data_span this[int i]
        {
            get
            {
                if (i >= 0 && i < Spans)
                {
                    return SpanData[i];
                }
                throw new IndexOutOfRangeException();
            }

        }

        public int Spans
        {
            get
            {
                if (Frozen)
                {
                    return FrozenSpanCount;
                }
                else
                {
                    return SpanData.Count;
                }
            }
        }

        public int TotalSamples
        {
            get
            {
                lock (this)
                {
                    int totalSamples = 0;
                    foreach (hrv_monitor_data_span span in SpanData)
                    {
                        totalSamples += span.Count;
                    }
                    return totalSamples;
                }
            }
        }

    }

    public class hrv_monitor_data_span
    {
        internal UInt32 NextSequence;
        private bool Frozen;
        private int FrozenSampleCount;
        private int SampleCount;
        private List<hrv_monitor_data_chunk> Chunks;

        internal hrv_monitor_data_span()
        {
            Chunks = new List<hrv_monitor_data_chunk>();
            SampleCount = 0;
            Frozen = false;
            NextSequence = 0;
        }

        internal void QueueSample(ref hrv_monitor_sample sample)
        {
            if(Chunks.Count > 0)
            {
                if(Chunks[Chunks.Count-1].HasSpace)
                {
                    Chunks[Chunks.Count - 1].AppendSample(ref sample);
                    SampleCount++;
                    return;
                }
            }
            // No room in existing chunk, add another.
            hrv_monitor_data_chunk chunk = new hrv_monitor_data_chunk();
            chunk.AppendSample(ref sample);
            Chunks.Add(chunk);
            SampleCount++;
        }

        internal void TrimEarlySamples(int samplesToDelete)
        {

        }

        internal void Freeze()
        {
            Frozen = true;
            FrozenSampleCount = SampleCount;
        }

        internal void Thaw()
        {
            Frozen = false;
        }


        public hrv_monitor_sample this[int i]
        {
            get
            {
                // Assumption: All but the last Chunk are always completely full.
                if (i >= 0 && i < Count)
                {
                    // Hopefully the compiler optimizes this for powers of 2 constants...
                    int chunkIndex = i / hrv_monitor_data_chunk.MaxSamples;
                    int chunkOffset = i % hrv_monitor_data_chunk.MaxSamples;
                    return Chunks[chunkIndex].Samples[chunkOffset];
                }
                throw new ArgumentOutOfRangeException();
            }
        }

        public int Count
        {
            get
            {
                if(Frozen)
                {
                    return FrozenSampleCount;
                }
                else
                {
                    return SampleCount;
                }
            }
        }
    }

    internal class hrv_monitor_data_chunk
    {
        public const int MaxSamples = 1024;

        public hrv_monitor_sample[] Samples;
        int StoredSamples;

        public hrv_monitor_data_chunk()
        {
            StoredSamples = 0;
            Samples = new hrv_monitor_sample[MaxSamples];
        }

        public bool HasSpace { get { return StoredSamples < MaxSamples; } }

        public void AppendSample(ref hrv_monitor_sample sample)
        {
            Samples[StoredSamples++] = sample;
        }

    }

    public struct hrv_monitor_sample
    {
        internal static bool CheckData(byte[] record)
        {
            // Magic number at the start of the record is 0xFFFF (yes, extremely original)
            if (record[0] != 0xFF) return false;
            if (record[1] != 0xFF) return false;

            for (int i = 7; i < 16; i += 2)
            {
                // The value readings are 14-bit, should not exceed 0x4000.
                if (record[i] > 0x3F) return false;
            }
            // Otherwise it's fine.
            return true;
        }

        public hrv_monitor_sample(byte[] record)
        {
            // Assume it has been checked.

            SequenceNumber = BitConverter.ToUInt32(record, 2);
            DarkValue = BitConverter.ToUInt16(record, 6);
            LED1Value = BitConverter.ToUInt16(record, 8);
            LED2Value = BitConverter.ToUInt16(record, 10);
            LED3Value = BitConverter.ToUInt16(record, 12);
            LED4Value = BitConverter.ToUInt16(record, 14);
        }

        public UInt32 SequenceNumber;
        public UInt16 DarkValue;
        public UInt16 LED1Value;
        public UInt16 LED2Value;
        public UInt16 LED3Value;
        public UInt16 LED4Value;


    }

}
