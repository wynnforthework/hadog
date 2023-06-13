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

using System.Collections.ObjectModel;
using System.Text;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace SDKTemplate
{
    // This scenario connects to the device selected in the "Discover
    // GATT Servers" scenario and communicates with it.
    // Note that this scenario is rather artificial because it communicates
    // with an unknown service with unknown characteristics.
    // In practice, your app will be interested in a specific service with
    // a specific characteristic.
    public sealed partial class Page_Register : Page
    {
        private MainPage RootPage { get; } = MainPage.Current;


        private AppDataHelper dataHelper;
        private const string KEY_Config = "config.txt";

        private ObservableCollection<DeviceEntity> RegisterDevices { get; } = new ObservableCollection<DeviceEntity>();

        #region UI Code
        public Page_Register()
        {
            InitializeComponent();
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.LoadConfig();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            
        }
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


        #region Pairing

        private void PairButton_Click()
        {
            var device = RegisterListView.SelectedItem as DeviceEntity;
            RegisterDevices.Remove(device);
            RootPage.NotifyUser($"不再判断设备：{device.Name}是否在家",
                        NotifyType.StatusMessage);
            this.SaveConfig();
        }

        #endregion

        #endregion


    }
}
