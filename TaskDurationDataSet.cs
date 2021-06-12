﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using VoxelTycoon.Serialization;
using VoxelTycoon.Tracks;
using VoxelTycoon.Tracks.Tasks;

namespace ScheduleStopwatch
{
    [SchemaVersion(2)]
    public class TaskDurationDataSet
    {
        private int _bufferSize;
        private readonly Dictionary<RootTask, DurationDataSet> _data = new Dictionary<RootTask, DurationDataSet>();

        protected DurationDataSet GetOrCreateDataSetForTask(RootTask task)
        {
            if (!_data.ContainsKey(task))
            {
                _data.Add(task, new DurationDataSet(_bufferSize));
            }

            return _data[task];
        }

        public TaskDurationDataSet(int bufferSize = DurationDataSet.DEFAULT_BUFFER_SIZE)
        {
            this._bufferSize = bufferSize;
        }

        public void Add(RootTask task, TimeSpan duration)
        {
            DurationDataSet dataSet = GetOrCreateDataSetForTask(task);
            dataSet.Add(duration);
        }

        public TimeSpan? GetAverageDuration(RootTask task)
        {
            if (!_data.TryGetValue(task, out DurationDataSet dataSet))
            {
                return null;
            }

            return dataSet.Average;
        }
        //Gets sum of average duration for all tasks in the list. If some task is missing data, returns null
        public TimeSpan? GetAverageDuration(IEnumerable<RootTask> tasks)
        {
            TimeSpan result = default;
            foreach (RootTask task in tasks)
            {
                TimeSpan? duration = GetAverageDuration(task);
                if (!duration.HasValue)
                {
                    return null;
                }
                result += duration.Value;
            }

            return result;
        }

        protected void CopyAverageValues(TaskDurationDataSet newDataSet, Vehicle newVehicle)
        {
            foreach (KeyValuePair<RootTask, DurationDataSet> pair in _data)
            {
                TimeSpan? avg = pair.Value.Average;
                if (avg != null)
                {
                    newDataSet.Add(newVehicle.Schedule.GetTasks()[pair.Key.GetIndex()], avg.Value);
                }
            }
        }

        public TaskDurationDataSet GetCopyWithAverageValues(Vehicle newVehicle, int dataBufferSize = 10)
        {
            TaskDurationDataSet result = new TaskDurationDataSet(dataBufferSize);
            CopyAverageValues(result, newVehicle);
            return result;
        }

        /* Add own average values into the provided dataset */
        public void AddAverageValuesToDataSet(TaskDurationDataSet data, Vehicle newVehicle)
        {
            foreach (KeyValuePair<RootTask, DurationDataSet> pair in _data)
            {
                TimeSpan? avg = pair.Value.Average;
                if (avg != null)
                {
                    data.Add(newVehicle.Schedule.GetTasks()[pair.Key.GetIndex()], avg.Value);
                }
            }
        }

        public virtual void OnStationRemoved(VehicleStationLocation station)
        {
            foreach (RootTask task in _data.Keys)
            {
                if (task.Destination.VehicleStationLocation == station)
                {
                    _data.Remove(task);
                }
            }
        }

        public virtual void Clear()
        {
            _data.Clear();
        }

        public virtual void Clear(RootTask task)
        {
            if (_data.TryGetValue(task, out DurationDataSet dataSet))
            {
                dataSet.Clear();
            }
        }

        /** marks task data for overwrite with next new data (all old data will be discarded when new data are added) */
        public void MarkForOverwrite(RootTask task)
        {
            if (_data.TryGetValue(task, out DurationDataSet dataSet))
            {
                dataSet.MarkForOverwrite();
            }
        }

        /** marks all task data for overwrite with next new data (all old data will be discarded when new data are added) */
        public void MarkAllForOverwrite()
        {
            foreach (DurationDataSet dataSet in _data.Values) { 
                dataSet.MarkForOverwrite();
            }
        }

        /** calculates the running average of all stored data and sets it as the new single record, marks it for overwrite with any new data and reduce number of elements for calculating running average */
        public void AdjustDataAfterCopy(int dataBufferSize = 10)
        {
            foreach (DurationDataSet dataSet in _data.Values)
            {
                dataSet.ReduceDataToSingleValue();
                dataSet.ChangeBufferSize(dataBufferSize);
                dataSet.MarkForOverwrite();
            }
        }
        public void ChangeBufferSize(int dataBufferSize = 10)
        {
            foreach (DurationDataSet dataSet in _data.Values)
            {
                _bufferSize = dataBufferSize;
                dataSet.ChangeBufferSize(dataBufferSize);
            }
        }

        public virtual void Remove(RootTask task)
        {
            _data.Remove(task);
        }

        internal virtual void Write(StateBinaryWriter writer)
        {
            writer.WriteInt(_data.Count);
            foreach (var pair in _data)
            {
                writer.WriteInt(pair.Key.GetIndex());
                pair.Value.Write(writer);
            }
        }
        internal static TaskDurationDataSet Read(StateBinaryReader reader, VehicleSchedule schedule) 
        {
            TaskDurationDataSet result = new TaskDurationDataSet();
            result.DoRead(reader, schedule);
            return result;
        }

        protected virtual void DoRead(StateBinaryReader reader, VehicleSchedule schedule)
        {
            int count = reader.ReadInt();

            for (int i = 0; i < count; i++)
            {
                int taskIndex = reader.ReadInt();
                RootTask task = schedule.GetTasks()[taskIndex];
                _data.Add(task, DurationDataSet.Read(reader));
            }
        }

    }
}
