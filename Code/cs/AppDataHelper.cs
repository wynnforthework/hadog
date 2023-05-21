using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDKTemplate
{
    public class AppDataHelper
    {
        #region 字段
        /// <summary>
        /// 获取应用的设置容器
        /// </summary>
        private Windows.Storage.ApplicationDataContainer LocalSettings { get; }

        /// <summary>
        /// 获取独立存储文件
        /// </summary>
        private Windows.Storage.StorageFolder LocalFolder { get; }
        #endregion

        public AppDataHelper()
        {
            LocalSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            LocalFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
        }

    #region Set应用设置(简单设置，复合设置，容器中的设置)
    /// <summary>
    /// 简单设置
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void SetValue(string key, string value)
        {
            LocalSettings.Values[key] = value;
        }

        /// <summary>
        /// 复合设置
        /// </summary>
        /// <param name="composite"></param>
        public void SetCompositeValue(Windows.Storage.ApplicationDataCompositeValue composite)
        {
            composite["intVal"] = 1;
            composite["strVal"] = "string";

            LocalSettings.Values["exampleCompositeSetting"] = composite;
        }

        /// <summary>
        /// 创建设置容器
        /// </summary>
        /// <param name="containerName"></param>
        /// <returns></returns>
        private Windows.Storage.ApplicationDataContainer CreateContainer(string containerName)
        {
            return LocalSettings.CreateContainer(containerName, Windows.Storage.ApplicationDataCreateDisposition.Always);
        }

        /// <summary>
        /// 讲设置保存到设置容器
        /// </summary>
        /// <param name="containerName"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SetContainerValue(string containerName, string key, string value)
        {
            if (!LocalSettings.Containers.ContainsKey(containerName))
                CreateContainer(containerName);

            LocalSettings.Containers[containerName].Values[key] = value;
        }
        #endregion

        #region Get应用设置(简单设置，复合设置，容器中的设置)

        /// <summary>
        /// 获取应用设置
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public object GetValue(string key)
        {
            return LocalSettings.Values[key];
        }

        /// <summary>
        /// 获取复合设置
        /// </summary>
        /// <param name="compositeKey"></param>
        /// <returns></returns>
        public Windows.Storage.ApplicationDataCompositeValue GetCompositeValue(string compositeKey)
        {
            // Composite setting
            Windows.Storage.ApplicationDataCompositeValue composite =
               (Windows.Storage.ApplicationDataCompositeValue)LocalSettings.Values[compositeKey];

            return composite;
        }

        /// <summary>
        /// 从设置容器中获取应用设置
        /// </summary>
        /// <returns></returns>
        public object GetValueByContainer(string containerName, string key)
        {
            bool hasContainer = LocalSettings.Containers.ContainsKey(containerName);

            if (hasContainer)
            {
                return LocalSettings.Containers[containerName].Values.ContainsKey(key);
            }
            return null;
        }
        #endregion

        #region Remove已完成的设置
        /// <summary>
        /// 删除简单设置或复合设置
        /// </summary>
        /// <param name="key"></param>
        public void Remove(string key)
        {
            LocalSettings.Values.Remove(key);
        }

        /// <summary>
        /// 删除设置容器
        /// </summary>
        /// <param name="key"></param>
        public void RemoveContainer(string containerName)
        {
            LocalSettings.DeleteContainer(containerName);
        }

        #endregion

        #region 文件存储操作

        /// <summary>
        /// 写入文件
        /// </summary>
        public async void WriteTimestamp(string fileName, string contents)
        {
            try
            {
                Windows.Storage.StorageFile sampleFile = await LocalFolder.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.ReplaceExisting);
                await Windows.Storage.FileIO.WriteTextAsync(sampleFile, contents);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// 读取文件
        /// </summary>
        public async Task<IList<string>> ReadTimestamp(string fileName)
        {
            try
            {
                Windows.Storage.StorageFile sampleFile = await LocalFolder.GetFileAsync(fileName);
                var contents = await Windows.Storage.FileIO.ReadLinesAsync(sampleFile);
                return contents;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return null;
            }
        }
        #endregion
    }
}
