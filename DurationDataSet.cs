﻿using System;
using Cyotek.Collections.Generic;
using HarmonyLib;
using VoxelTycoon.Serialization;

namespace ScheduleStopwatch
{
    [SchemaVersion(3)]
    public class DurationDataSet
    {
        public const int DEFAULT_BUFFER_SIZE = 10;
        private TimeSpan? _average;

        private TimeSpan _totalSum = default;
        private TimeSpan _runningSum = default;
        private int _totalCount;
        private bool _toOverwrite;

        private CircularBuffer<TimeSpan> _durationData;
        public DurationDataSet(int bufferSize = DEFAULT_BUFFER_SIZE)
        {
            _durationData = new CircularBuffer<TimeSpan>(bufferSize, false);
        }

        public void MarkDirty()
        {
            _average = null;
        }

        public void Clear()
        {
            _durationData.Clear();
            _totalSum = default;
            _totalCount = 0;
            _runningSum = default;
            _toOverwrite = false;
            MarkDirty();
        }

        /** marks task data for overwrite with next new data (all old data will be discarded when new data are added) */
        public void MarkForOverwrite()
        {
            _toOverwrite = true;
        }

        public bool Estimated
        {
            get
            {
                return _durationData.Size == 0 || _toOverwrite == true;
            }
        }

        /** calculates the running average of all stored data and sets it as the new single record */
        public void ReduceDataToSingleValue()
        {
            if (_durationData.Size > 1)
            {
                TimeSpan? average = Average;
                Clear();
                Add(average.Value);
            }
        }

        public void ChangeBufferSize(int bufferSize = DEFAULT_BUFFER_SIZE)
        {
            if (bufferSize != _durationData.Capacity)
            {
                while (bufferSize < _durationData.Size)
                {
                    _runningSum -= _durationData.Get();
                }
                _durationData.Capacity = bufferSize;
            }
        }

        private TimeSpan CalcSum()
        {
            TimeSpan sum = default;
            for (int i = 0; i < _durationData.Size; i++) { 
                sum += _durationData.PeekAt(i);
            }
            return sum;
        }

        public void Add(TimeSpan duration)
        {
            if (_toOverwrite)
            {
                Clear();
            } else
            if (_durationData.IsFull)
            {
                _runningSum -= _durationData.Get();
            }
            _durationData.Put(duration);
            _totalCount++;
            _totalSum += duration;
            _runningSum += duration;
/*            TimeSpan calc = CalcSum();
            if (_runningSum != calc)
            {
                FileLog.Log("Running != Calc: " + _runningSum.TotalHours.ToString("N2") + " vs " + calc.TotalHours.ToString("N2"));
            }*/
            MarkDirty();
        }

        public TimeSpan? Average
        {
            get
            {
                if (_average == null)
                {
                    if (_durationData.Size == 0)
                    {
                        return null;
                    }
                    _average = new TimeSpan(_runningSum.Ticks / _durationData.Size);
                }
                return _average;
            }
        }

        public TimeSpan? TotalAverage
        {
            get
            {
                return (_totalCount > 0) ? new TimeSpan?(new TimeSpan(_totalSum.Ticks / _totalCount)) : null;
            }
        }

        public void Write(StateBinaryWriter writer)
        {
            writer.WriteInt(_durationData.Size);
            foreach (TimeSpan duration in _durationData)
            {
                writer.WriteLong(duration.Ticks);
            }
            writer.WriteInt(_totalCount);
            writer.WriteLong(_totalSum.Ticks);
        }

        internal static DurationDataSet Read(StateBinaryReader reader)
        {
            DurationDataSet result = new DurationDataSet();
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                TimeSpan span = new TimeSpan(reader.ReadLong());
                result._durationData.Put(span);
                result._runningSum += span;
            }

            result._totalCount = reader.ReadInt();
            result._totalSum = new TimeSpan(reader.ReadLong());

            return result;
        }

    }
}
