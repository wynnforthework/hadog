//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using MQTTnet;
using MQTTnet.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace SDKTemplate
{
    // This scenario uses a DeviceWatcher to enumerate nearby Bluetooth Low Energy devices,
    // displays them in a ListView, and lets the user select a device and pair it.
    // This device will be used by future scenarios.
    // For more information about device discovery and pairing, including examples of
    // customizing the pairing process, see the DeviceEnumerationAndPairing sample.
    public sealed partial class Page_Discovery : Page
    {
        private MainPage RootPage { get; } = MainPage.Current;

        private ObservableCollection<BluetoothLEDeviceDisplay> KnownDevices { get; } = new ObservableCollection<BluetoothLEDeviceDisplay>();
        private List<DeviceInformation> UnknownDevices { get; } = new List<DeviceInformation>();

        private DeviceWatcher deviceWatcher;

        private ObservableCollection<DeviceEntity> RegisterDevices { get; } = new ObservableCollection<DeviceEntity>();


        private AppDataHelper dataHelper;
        private const string KEY_Config = "config.txt";

        #region UI Code
        public Page_Discovery()
        {
            InitializeComponent();
            this.CreateMqttClient();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.LoadConfig();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            StopBleDeviceWatcher();

            // Save the selected device's ID for use in other scenarios.
            if (ResultsListView.SelectedItem is BluetoothLEDeviceDisplay bleDeviceDisplay)
            {
                RootPage.SelectedBleDeviceId = bleDeviceDisplay.Id;
                RootPage.SelectedBleDeviceName = bleDeviceDisplay.Name;
            }

        }

        private void EnumerateButton_Click()
        {
            if (deviceWatcher == null)
            {
                StartBleDeviceWatcher();
                EnumerateButton.Content = "停止扫描";
                RootPage.NotifyUser($"Device watcher started.", NotifyType.StatusMessage);
            }
            else
            {
                StopBleDeviceWatcher();
                EnumerateButton.Content = "开始扫描";
                RootPage.NotifyUser($"Device watcher stopped.", NotifyType.StatusMessage);
            }
        }

        private bool Not(bool value) => !value;

        #endregion

        #region Device discovery

        /// <summary>
        /// Starts a device watcher that looks for all nearby Bluetooth devices (paired or unpaired). 
        /// Attaches event handlers to populate the device collection.
        /// </summary>
        private void StartBleDeviceWatcher()
        {
            // Additional properties we would like about the device.
            // Property strings are documented here https://msdn.microsoft.com/en-us/library/windows/desktop/ff521659(v=vs.85).aspx
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable", "System.Devices.Aep.IsPresent" };

            // BT_Code: Example showing paired and non-paired in a single query.
            //string aqsAllBluetoothLEDevices = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";
            string aqsAllBluetoothLEDevices = "(System.Devices.Aep.CanPair:=true)";

            deviceWatcher =
                    DeviceInformation.CreateWatcher(
                        aqsAllBluetoothLEDevices,
                        requestedProperties,
                        DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            deviceWatcher.Removed += DeviceWatcher_Removed;
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            deviceWatcher.Stopped += DeviceWatcher_Stopped;

            // Start over with an empty collection.
            KnownDevices.Clear();

            // Start the watcher. Active enumeration is limited to approximately 30 seconds.
            // This limits power usage and reduces interference with other Bluetooth activities.
            // To monitor for the presence of Bluetooth LE devices for an extended period,
            // use the BluetoothLEAdvertisementWatcher runtime class. See the BluetoothAdvertisement
            // sample for an example.
            deviceWatcher.Start();
        }

        /// <summary>
        /// Stops watching for all nearby Bluetooth devices.
        /// </summary>
        private void StopBleDeviceWatcher()
        {
            if (deviceWatcher != null)
            {
                // Unregister the event handlers.
                deviceWatcher.Added -= DeviceWatcher_Added;
                deviceWatcher.Updated -= DeviceWatcher_Updated;
                deviceWatcher.Removed -= DeviceWatcher_Removed;
                deviceWatcher.EnumerationCompleted -= DeviceWatcher_EnumerationCompleted;
                deviceWatcher.Stopped -= DeviceWatcher_Stopped;

                // Stop the watcher.
                deviceWatcher.Stop();
                deviceWatcher = null;
            }
        }

        private BluetoothLEDeviceDisplay FindBluetoothLEDeviceDisplay(string id)
        {
            foreach (BluetoothLEDeviceDisplay bleDeviceDisplay in KnownDevices)
            {
                if (bleDeviceDisplay.Id == id)
                {
                    return bleDeviceDisplay;
                }
            }
            return null;
        }

        private DeviceInformation FindUnknownDevices(string id)
        {
            foreach (DeviceInformation bleDeviceInfo in UnknownDevices)
            {
                if (bleDeviceInfo.Id == id)
                {
                    return bleDeviceInfo;
                }
            }
            return null;
        }

        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    Debug.WriteLine(String.Format("Added {0}{1}", deviceInfo.Id, deviceInfo.Name));

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        this.CheckHomeState(TopicEnum.GetHome, deviceInfo.Id);
                        
                        // Make sure device isn't already present in the list.
                        if (FindBluetoothLEDeviceDisplay(deviceInfo.Id) == null)
                        {
                            if (deviceInfo.Name != string.Empty)
                            {
                                // If device has a friendly name display it immediately.
                                var dd = new BluetoothLEDeviceDisplay(deviceInfo);
                                var rd = RegisterDevices.FirstOrDefault((d) => d.Id.Equals(deviceInfo.Id));
                                dd.IsPaired = rd != null;
                                KnownDevices.Add(dd);
                            }
                            else
                            {
                                // Add it to a list in case the name gets updated later. 
                                UnknownDevices.Add(deviceInfo);
                            }
                        }

                    }
                }
            });
        }

        private async void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    Debug.WriteLine(String.Format("Updated {0}{1}", deviceInfoUpdate.Id, ""));

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                        if (bleDeviceDisplay != null)
                        {
                            // Device is already being displayed - update UX.
                            bleDeviceDisplay.Update(deviceInfoUpdate);
                            return;
                        }

                        DeviceInformation deviceInfo = FindUnknownDevices(deviceInfoUpdate.Id);
                        if (deviceInfo != null)
                        {
                            deviceInfo.Update(deviceInfoUpdate);
                            // If device has been updated with a friendly name it's no longer unknown.
                            if (deviceInfo.Name != String.Empty)
                            {
                                KnownDevices.Add(new BluetoothLEDeviceDisplay(deviceInfo));
                                UnknownDevices.Remove(deviceInfo);
                            }
                        }
                    }
                }
            });
        }

        private async void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    Debug.WriteLine(String.Format("Removed {0}{1}", deviceInfoUpdate.Id,""));

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        this.CheckHomeState(TopicEnum.LeaveHome, deviceInfoUpdate.Id);

                        // Find the corresponding DeviceInformation in the collection and remove it.
                        BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                        if (bleDeviceDisplay != null)
                        {
                            KnownDevices.Remove(bleDeviceDisplay);
                        }

                        DeviceInformation deviceInfo = FindUnknownDevices(deviceInfoUpdate.Id);
                        if (deviceInfo != null)
                        {
                            UnknownDevices.Remove(deviceInfo);
                        }
                    }
                }
            });
        }

        private async void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object e)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcher)
                {
                    RootPage.NotifyUser($"{KnownDevices.Count} devices found. Enumeration completed.",
                        NotifyType.StatusMessage);
                }
            });
        }

        private async void DeviceWatcher_Stopped(DeviceWatcher sender, object e)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcher)
                {
                    RootPage.NotifyUser($"No longer watching for devices.",
                            sender.Status == DeviceWatcherStatus.Aborted ? NotifyType.ErrorMessage : NotifyType.StatusMessage);
                }
            });
        }
        #endregion

        #region Pairing

        private void PairButton_Click()
        {
            var bleDeviceDisplay = ResultsListView.SelectedItem as BluetoothLEDeviceDisplay;
            bleDeviceDisplay.IsPaired = true;
            RegisterDevices.Add(new DeviceEntity(bleDeviceDisplay.Id,bleDeviceDisplay.Name));
            this.SaveConfig();
        }

        #endregion

        #region MQTT

        private IMqttClient mqttClient;
        private MqttClientOptions options;

        private void CreateMqttClient()
        {
            options = new MqttClientOptionsBuilder()
                        .WithClientId("回家/离家判断")
                        .WithTcpServer("192.168.31.111", 1883)
                        .WithCredentials("mqtt", "1111")
                        .WithTls(new MqttClientOptionsBuilderTlsParameters()
                        {
                            UseTls = false,
                            SslProtocol = SslProtocols.None
                        })
                        .WithCleanSession()
                        .Build();
            var factory = new MqttFactory();
            mqttClient = factory.CreateMqttClient();
        }

        public enum TopicEnum
        {
            Test = 0,
            LeaveHome,
            GetHome
        }

        private async void TopicTest()
        {
            if (!mqttClient.IsConnected)
            {
                await mqttClient.ConnectAsync(options);
            }
            var topic = "home/small/light";
            await mqttClient.PublishStringAsync(topic).ConfigureAwait(false);
            RootPage.NotifyUser($"Send Topic:{topic}", NotifyType.StatusMessage);
        }

        private List<TopicEnum> TopicList = new List<TopicEnum>();
        private void CheckHomeState(TopicEnum @enum, string id)
        {
            var device = RegisterDevices.FirstOrDefault((d) => d.Id.Equals(id));
            if (device != null)
            {
                bool flag = false;
                switch (@enum)
                {
                    case TopicEnum.LeaveHome:
                        {
                            device.IsAtHome = false;
                            var atHome = RegisterDevices.FirstOrDefault((d) => d.IsAtHome);
                            flag = atHome == null;
                        }
                        break;
                    case TopicEnum.GetHome:
                        {
                            var atHome = RegisterDevices.FirstOrDefault((d) => d.IsAtHome);
                            device.IsAtHome = true;
                            flag = atHome == null;
                        }
                        break;
                }
                if (flag)
                {
                    this.TopicList.Add(@enum);
                    this.DoNextTopic();
                }
            }
        }

        private bool IsBusy = false;
        private async void DoNextTopic()
        {
            if(this.TopicList.Count>0)
            {
                this.SwitchStateMachine();

                if (!this.IsBusy)
                {
                    this.IsBusy = true;
                }

                var topic = this.TopicList[0];
                this.TopicList.RemoveAt(0);
                await this.SendTopic(topic);
                this.ChangeStateMachine(StateMachineEnum.None);
                this.IsBusy = false;
                this.DoNextTopic();
            }
        }

        private async Task SendTopic(TopicEnum @enum)
        {
            if(@enum == TopicEnum.LeaveHome)
            {
                //等待30秒
                await Task.Delay(3 * 1000);
            }

            string topic = "";
            switch(@enum)
            {
                case TopicEnum.LeaveHome:
                    {
                        topic = "home/small/leavehome";
                    }
                    break;
                case TopicEnum.GetHome:
                    {
                        topic = "home/small/gethome";
                    }
                    break;
            }
            if (!mqttClient.IsConnected)
            {

                for (var i = 0; i < 10; i++)
                {
                    var isConnected = false;
                    try
                    {
                        await mqttClient.ConnectAsync(options);
                        isConnected = mqttClient.IsConnected;
                    }
                    catch (Exception e)
                    {
                        var log = "";
                        if (i == 0)
                        {
                            log = $"MQTT连接失败:{e.Message}，等待重连!";
                        }
                        else if (i == 10)
                        {
                            log = $"MQTT重连失败";
                        }
                        else
                        {
                            log = $"等待MQTT第{i}次重连";
                        }
                        Debug.WriteLine(log);
                        RootPage.NotifyUser(log, NotifyType.StatusMessage);

                        //等待10秒
                        await Task.Delay(10 * 1000);
                    }
                    if (isConnected)
                    {
                        break;
                    }
                }
            }

            if (this.StateMachine == StateMachineEnum.None)
            {
                return;
            }
            Debug.WriteLine($"{topic}");

            await mqttClient.PublishStringAsync(topic).ConfigureAwait(false);
            RootPage.NotifyUser($"Send Topic:{topic}", NotifyType.StatusMessage);
        }

        #endregion

        #region 状态机

        public enum StateMachineEnum
        {
            None = 0,
            WaitSendTopic,
        }

        private StateMachineEnum StateMachine = StateMachineEnum.None;
        private void ChangeStateMachine(StateMachineEnum @enum)
        {
            this.StateMachine = @enum;
        }
        private void SwitchStateMachine()
        {
            Debug.WriteLine($"1:{this.StateMachine}");
            switch(this.StateMachine)
            {
                case StateMachineEnum.None:

                    this.StateMachine = StateMachineEnum.WaitSendTopic;
                    break;
                case StateMachineEnum.WaitSendTopic:
                    this.StateMachine = StateMachineEnum.None;
                    break;
            }
            Debug.WriteLine($"2:{this.StateMachine}");
        }

        #endregion

        #region 配置
        private async void LoadConfig()
        {
            dataHelper = new AppDataHelper();
            var devices = await dataHelper.ReadTimestamp(KEY_Config);
            if (devices != null)
            {
                foreach (var device in devices)
                {
                    RegisterDevices.Add(new DeviceEntity(device));
                }
            }
        }

        private void SaveConfig()
        {
            if (RegisterDevices.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var device in RegisterDevices)
                {
                    sb.Append(device.ToString());
                    sb.Append("\n");
                }
                dataHelper.WriteTimestamp(KEY_Config, sb.ToString());
            }
        }

        #endregion

        #region 单元测试

        /// <summary>
        /// 正常回家
        /// 结果:触发回家
        /// </summary>
        private void Test_0()
        {
            this.TopicList.Clear();
            this.TopicList.Add(TopicEnum.GetHome);
            this.DoNextTopic();
        }

        /// <summary>
        /// 正常离家
        /// 结果:触发离家
        /// </summary>
        private void Test_1()
        {
            this.TopicList.Clear();
            this.TopicList.Add(TopicEnum.LeaveHome);
            this.DoNextTopic();

        }

        /// <summary>
        /// 正常回家,等一段时间后
        /// 正常离家
        /// 结果:触发回家,触发离家
        /// </summary>
        private void Test_2()
        {
            this.TopicList.Clear();
            this.TopicList.Add(TopicEnum.GetHome);
            this.TopicList.Add(TopicEnum.LeaveHome);
            this.DoNextTopic();
        }

        /// <summary>
        /// 正常离家,等一段时间后
        /// 正常回家
        /// 结果:触发离家,触发回家
        /// </summary>
        private void Test_3()
        {
            this.TopicList.Clear();
            this.TopicList.Add(TopicEnum.LeaveHome);
            this.TopicList.Add(TopicEnum.GetHome);
            this.DoNextTopic();
        }

        /// <summary>
        /// 正常回家,等一段时间后
        /// 正常离家,等一段时间后
        /// 正常回家
        /// 结果:触发回家,触发离家,触发回家
        /// </summary>
        private void Test_4()
        {
            this.TopicList.Clear();
            this.TopicList.Add(TopicEnum.GetHome);
            this.TopicList.Add(TopicEnum.LeaveHome);
            this.TopicList.Add(TopicEnum.GetHome);
            this.DoNextTopic();
        }

        /// <summary>
        /// 正常回家
        /// 短时间内离家
        /// 结果:触发回家,触发离家
        /// </summary>
        private void Test_5()
        {
            this.TopicList.Clear();
            this.TopicList.Add(TopicEnum.GetHome);
            this.DoNextTopic();
            this.TopicList.Add(TopicEnum.LeaveHome);
            this.DoNextTopic();
        }
        /// <summary>
        /// 正常离家
        /// 短时间内回家
        /// 结果:什么都不做
        /// </summary>
        private void Test_6()
        {
            this.TopicList.Clear();
            this.TopicList.Add(TopicEnum.LeaveHome);
            this.DoNextTopic();
            this.TopicList.Add(TopicEnum.GetHome);
            this.DoNextTopic();
        }
        /// <summary>
        /// 正常回家,等一段时间后
        /// 短时间内离家-回家
        /// 结果:只触发1次回家
        /// </summary>
        private void Test_7()
        {
            this.TopicList.Clear();
            this.TopicList.Add(TopicEnum.GetHome);
            this.DoNextTopic();
            this.TopicList.Add(TopicEnum.LeaveHome);
            this.DoNextTopic();
            this.TopicList.Add(TopicEnum.GetHome);
            this.DoNextTopic();
        }
        #endregion
    }
}