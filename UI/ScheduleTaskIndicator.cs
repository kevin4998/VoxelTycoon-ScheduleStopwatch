﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using VoxelTycoon;
using VoxelTycoon.Localization;
using VoxelTycoon.Tracks.Tasks;
using VoxelTycoon.UI;
using static ScheduleStopwatch.TaskDurationDataSet;
using static ScheduleStopwatch.VehicleScheduleCapacity;

namespace ScheduleStopwatch.UI
{
    class ScheduleTaskIndicator : ScheduleIndicator
    {
        public RootTask Task { get; private set; }

        public static ScheduleTaskIndicator GetInstance(Transform parent)
        {
            if (_template == null)
            {
                CreateTemplate();
            }
            return UnityEngine.Object.Instantiate<ScheduleTaskIndicator>(_template, parent);
        }

        public void Initialize(RootTask task, VehicleScheduleData data, Settings settings)
        {
            Task = task;
            _scheduleData = data;
            transform.name = "StopWatchDuration";
            Transform timeIndicator = transform.Find("TimeIndicator");

            if (settings.ShowIndividualTaskTimes)
            {
                _travelTimeText = timeIndicator.Find("TravelTimeText").GetComponent<Text>();
                _loadingTimeText = timeIndicator.Find("LoadingTimeText").GetComponent<Text>();
                Tooltip.For(
                    timeIndicator,
                    LazyManager<LocaleManager>.Current.Locale.GetString("schedule_stopwatch/task_times_hint")                    
                );
            }
            else
            {
                timeIndicator.gameObject.SetActive(false);
            }

            if (settings.ShowIndividualLoadingCapacity)
            {
                _loadingCapIcon = transform.Find("LoadingCapacityIcon");
                _loadCapacityIndicator = transform.Find("CargoCapacityLoad").GetComponent<CargoCapacityIndicator>();
                _loadCapacityIndicator.Initialize(null, null);

                Tooltip.For(
                    _loadCapacityIndicator,
                    () => GetCapacityTooltipText(),
                    null
                );
            }

            if (settings.ShowIndividualUnloadingCapacity)
            {
                _unloadCapacityIndicator = transform.Find("CargoCapacityUnload").GetComponent<CargoCapacityIndicator>();
                _unloadingCapIcon = transform.Find("UnloadingCapacityIcon");
                _unloadCapacityIndicator.Initialize(null, null);

                Tooltip.For(
                    _unloadCapacityIndicator,
                    () => GetCapacityTooltipText(),
                    null
                );
            }
            transform.gameObject.SetActive(true);
        }

        public void UpdateValues(VehicleScheduleData data, RootTask _)
        {
            if (data != _scheduleData || data == null)
            {
                throw new ArgumentException("Schedule data is not for this ScheduleTaskIndicator");
            }
            Locale locale = LazyManager<LocaleManager>.Current.Locale;

            if (_travelTimeText != null)
            {
                DurationData travel = _lastTravelData = data.GetAverageTravelDuration(Task);
                if (travel != null)
                {
                    _travelTimeText.text = locale.GetString("schedule_stopwatch/days_hours").Format(((int)travel.Duration.TotalDays).ToString("N0"), travel.Duration.Hours.ToString("N0"));
                    if (travel.Estimated)
                    {
                        _travelTimeText.color = Color.gray;
                    } else
                    {
                        _travelTimeText.color = Color.black;
                    }
                }
                else
                {
                    _travelTimeText.text = locale.GetString("schedule_stopwatch/unknown").ToUpper();
                }
                DurationData loading = _lastStationLoadingData = data.GetAverageStationLoadingDuration(Task);
                if (loading != null)
                {
                    _loadingTimeText.text = locale.GetString("schedule_stopwatch/hours_minutes").Format(((int)loading.Duration.TotalHours).ToString("N0"), loading.Duration.Minutes.ToString("N0"));
                    if (loading.Estimated)
                    {
                        _loadingTimeText.color = Color.grey;
                    } else
                    {
                        _loadingTimeText.color = Color.black;
                    }
                }
                else
                {
                    _loadingTimeText.text = locale.GetString("schedule_stopwatch/unknown").ToUpper();
                }
            }

            if (_loadCapacityIndicator != null || _unloadCapacityIndicator != null)
            {
                _lastMonthMultiplier = data.ScheduleMonthlyMultiplier;
                _lastTaskTransfers = _scheduleData.Capacity.GetTransfers(Task);
                IReadOnlyDictionary<Item, TransferData> routeTransfers = RouteTaskTransfers;
                int itemsLimit;
                if (routeTransfers != null && routeTransfers.Count>0)
                {
                    itemsLimit = _travelTimeText != null ? 2 : 5;
                } else
                {
                    itemsLimit = _travelTimeText != null ? 4 : 8;
                }

                if (_unloadCapacityIndicator != null)
                {
                    _unloadCapacityIndicator.UpdateItems(_lastTaskTransfers, _lastMonthMultiplier, routeTransfers, transfDirection: TransferDirection.unloading, itemsLimit);
                    _unloadCapacityIndicator.gameObject.SetActive(_unloadCapacityIndicator.ItemsCount > 0);
                    _unloadingCapIcon.gameObject.SetActive(_unloadCapacityIndicator.ItemsCount > 0);
                    itemsLimit -= Math.Min(_unloadCapacityIndicator.ItemsCount, 1);
                }


                if (_loadCapacityIndicator != null)
                {
                    _loadCapacityIndicator.UpdateItems(_lastTaskTransfers, _lastMonthMultiplier, routeTransfers, transfDirection: TransferDirection.loading, itemsLimit);
                    _loadCapacityIndicator.gameObject.SetActive(_loadCapacityIndicator.ItemsCount > 0);
                    _loadingCapIcon.gameObject.SetActive(_loadCapacityIndicator.ItemsCount > 0);
                }

            }
        }

        private string GetCapacityTooltipText()
        {
            Locale locale = LazyManager<LocaleManager>.Current.Locale;
            if (_lastMonthMultiplier != null && _lastTaskTransfers != null && _lastTaskTransfers.Count > 0)
            {
                IReadOnlyDictionary<Item, TransferData> routeTaskTransfers = RouteTaskTransfers;
                bool isRoute = routeTaskTransfers != null && routeTaskTransfers.Count > 0;
                StringBuilder sb = new StringBuilder();
                sb.Append(StringHelper.Boldify(locale.GetString("schedule_stopwatch/estim_monthly_transf").ToUpper()));
                if (isRoute)
                {
                    sb.AppendLine().Append(StringHelper.Colorify(locale.GetString("schedule_stopwatch/estim_monthly_transf_hint"), UIColors.Solid.Text * 0.5f));
                }
                if (_lastTravelData?.Estimated == true || _lastStationLoadingData?.Estimated == true)
                {
                    sb.AppendLine().Append(StringHelper.Colorify(locale.GetString("schedule_stopwatch/inaccurate"), UIColors.Solid.Text * 0.5f));
                }
                ScheduleCapacityHelper.TooltipTextForStation(_lastTaskTransfers, sb, routeTaskTransfers, _lastMonthMultiplier.Value);
                return sb.ToString();
            }
            return "";
        }

        private static void CreateTemplate()
        { 
            var baseTemplate = UnityEngine.Object.Instantiate<Transform>(ScheduleIndicator.BaseTemplate);
            baseTemplate.gameObject.SetActive(false);
            _template = baseTemplate.gameObject.AddComponent<ScheduleTaskIndicator>();
            Transform timeContainer = baseTemplate.Find("TimeIndicator");
            timeContainer.gameObject.AddComponent<CanvasRenderer>();
            timeContainer.gameObject.AddComponent<NonDrawingGraphic>();
            Transform iconTransform = timeContainer.transform.Find("Icon");
            iconTransform.name = "TravelTimeIcon";
            Text iconText = iconTransform.GetComponent<Text>();
            iconText.text = "";
            iconText.font = R.Fonts.Ketizoloto;

            Transform textTransform = timeContainer.transform.Find("Text");
            textTransform.name = "TravelTimeText";
            Text text = textTransform.GetComponent<Text>();
            text.text = LazyManager<LocaleManager>.Current.Locale.GetString("schedule_stopwatch/unknown").ToUpper();

            Transform loadingIcon = UnityEngine.Object.Instantiate<Transform>(iconTransform, iconTransform.parent);
            loadingIcon.name = "LoadingTimeIcon";
            Text iconText2 = loadingIcon.GetComponent<Text>();
            iconText2.text = "";

            Transform loadingTextTransf = UnityEngine.Object.Instantiate<Transform>(textTransform, textTransform.parent);
            loadingTextTransf.name = "LoadingTimeText";

            Transform unloadingCapIcon = UnityEngine.Object.Instantiate<Transform>(iconTransform, baseTemplate.transform);
            unloadingCapIcon.SetActive(false);
            unloadingCapIcon.name = "UnloadingCapacityIcon";
            Text iconText4 = unloadingCapIcon.GetComponent<Text>();
            iconText4.font = R.Fonts.FontAwesome5FreeSolid900;
            iconText4.text = "";

            CargoCapacityIndicator unloadIndicator = CargoCapacityIndicator.GetInstance(baseTemplate.transform);
            unloadIndicator.transform.name = "CargoCapacityUnload";

            Transform loadingCapIcon = UnityEngine.Object.Instantiate<Transform>(iconTransform, baseTemplate.transform);
            loadingCapIcon.gameObject.SetActive(false);
            loadingCapIcon.name = "LoadingCapacityIcon";
            Text iconText3 = loadingCapIcon.GetComponent<Text>();
            iconText3.font = R.Fonts.FontAwesome5FreeSolid900;
            iconText3.text = "";

            CargoCapacityIndicator loadIndicator = CargoCapacityIndicator.GetInstance(baseTemplate.transform);
            loadIndicator.transform.name = "CargoCapacityLoad";
        }

        private IReadOnlyDictionary<Item, TransferData> RouteTaskTransfers
        {
            get
            {
                if (_lastTaskTransfers != null && _scheduleData.Vehicle.Route?.Vehicles.Count > 0)
                {
                    return LazyManager<RouteCapacityCache>.Current.GetRouteTaskTransfers(_scheduleData.Vehicle.Route, Task);
                }
                return null;
            }
        }

        protected void OnEnable()
        {
            if (_scheduleData != null)
            {
                _scheduleData.SubscribeDataChanged(UpdateValues);
//                _scheduleData.SubscribeTaskDataChanged(Task, UpdaeValues);

                UpdateValues(_scheduleData, Task);
            }
        }

        protected void OnDisable()
        {
            if (_scheduleData != null && Task != null)
            {
                _scheduleData.UnsubscribeDataChanged(UpdateValues);
                _scheduleData.UnsubscribeTaskDataChanged(Task, UpdateValues);
            }
        }

        private Text _travelTimeText, _loadingTimeText;
        private float? _lastMonthMultiplier;
        private DurationData _lastTravelData;
        private DurationData _lastStationLoadingData;
        private IReadOnlyDictionary<Item, TransferData> _lastTaskTransfers;
        private static ScheduleTaskIndicator _template;
        private VehicleScheduleData _scheduleData;
        private CargoCapacityIndicator _loadCapacityIndicator, _unloadCapacityIndicator;
        private Transform _loadingCapIcon, _unloadingCapIcon;

    }
}
