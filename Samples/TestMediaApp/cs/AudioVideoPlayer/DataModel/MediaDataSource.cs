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
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Networking.BackgroundTransfer;
using Windows.Security.Cryptography.Core;
using Windows.Storage;
using Newtonsoft.Json;

namespace AudioVideoPlayer.DataModel
{
    /// <summary>
    /// Audio Video item data model.
    /// </summary>
    public class MediaItem
    {
        public MediaItem(String uniqueId,
                              String comment,
                              String title,
                              String imagePath,
                              String description,
                              String content,
                              String posterContent,
                              long start,
                              long duration,
                              String httpHeaders,
                              String playReadyUrl,
                              String playReadyCustomData,
                              bool backgroundAudio)

        {
            this.UniqueId = uniqueId;
            this.Comment = comment;
            this.Title = title;
            this.Description = description;
            this.ImagePath = imagePath;
            this.Content = content;
            this.PosterContent = posterContent;
            this.Start = start;
            this.Duration = duration;
            this.HttpHeaders = httpHeaders;
            this.PlayReadyUrl = playReadyUrl;
            this.PlayReadyCustomData = playReadyCustomData;
            this.BackgroundAudio = backgroundAudio;
        }
        public string UniqueId { get; private set; }
        public string Comment { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public string ImagePath { get; private set; }
        public string Content { get; private set; }
        public string PosterContent { get; private set; }
        public long Start { get; private set; }
        public long Duration { get; private set; }
        public string HttpHeaders { get; private set; }
        public string PlayReadyUrl { get; private set; }
        public string PlayReadyCustomData { get; private set; }
        public bool BackgroundAudio { get; private set; }
        public override string ToString()
        {
            return this.Title;
        }
    }

    /// <summary>
    /// Audio Video group data model.
    /// </summary>
    public class MediaDataGroup
    {
        public MediaDataGroup(String uniqueId,
                               String title,
                               String category,
                               String imagePath,
                               String description)
        {
            this.UniqueId = uniqueId;
            this.Title = title;
            this.Category = category;
            this.Description = description;
            this.ImagePath = imagePath;
            this.Items = new ObservableCollection<MediaItem>();
        }

        public string UniqueId { get; private set; }
        public string Title { get; private set; }
        public string Category { get; private set; }
        public string Description { get; private set; }
        public string ImagePath { get; private set; }
        public ObservableCollection<MediaItem> Items { get; private set; }

        public override string ToString()
        {
            return this.Title;
        }
    }

    public class PlaylistConfiguration
    {
        public string Url { get; set; }
        public string Mail { get; set; }
    }

    public class Video
    {
        public string Id { get; set; }
        public string Url { get; set; }
    }

    class MediaDataSource
    {
        public static string MediaDataPath { get; private set; }

        private const string downloadFileName = "downloadedVideos.json";
        private static Dictionary<string, string> _localVideoReady = new Dictionary<string, string>();
        private static List<Video> _toDownload = new List<Video>();

        private static MediaDataSource _MediaDataSource = new MediaDataSource();

        public static async Task<int> ExtractVideos()
        {
            var downloadedVideos = await ApplicationData.Current.LocalFolder.TryGetItemAsync("downloadedVideos.json");
            if (downloadedVideos != null)
            {
                var videosFile = await FileIO.ReadTextAsync(await ApplicationData.Current.LocalFolder.GetFileAsync(downloadFileName));
                var videos = JsonConvert.DeserializeObject<List<Video>>(videosFile);
                foreach (var video in videos)
                {
                    _localVideoReady[video.Id] = video.Url;
                }
                return videos.Count;
            }
            return 0;
        }

        public static async Task<int> SaveVideos()
        {
            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(downloadFileName, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, JsonConvert.SerializeObject(_localVideoReady.Select(x => new Video { Id = x.Key, Url = x.Value })));
            return _localVideoReady.Count;
        }

        private ObservableCollection<MediaDataGroup> _groups = new ObservableCollection<MediaDataGroup>();
        public ObservableCollection<MediaDataGroup> Groups
        {
            get { return this._groups; }
        }

        public static async Task<MediaDataGroup> GetGroupAsync(string path, string uniqueId)
        {
            if (await _MediaDataSource.GetMediaDataAsync(path) == true)
            {
                // Simple linear search is acceptable for small data sets
                var matches = _MediaDataSource.Groups.Where((group) => group.UniqueId.Equals(uniqueId));
                if (matches.Count() == 1) return matches.First();
            }
            return null;
        }

        public static void Clear()
        {
            if (_MediaDataSource._groups.Count != 0)
            {
                _MediaDataSource._groups.Clear();
            }
        }

        private async Task<bool> GetMediaDataAsync(string path)
        {
            //if (this._groups.Count != 0)
            //    return false;
            string jsonText = string.Empty;

            if (string.IsNullOrEmpty(path))
            {
                // load the default data
                //If retrieving json from web failed then use embedded json data file.
                if (string.IsNullOrEmpty(jsonText))
                {
                    Uri dataUri = new Uri("ms-appx:///DataModel/MediaData.json");

                    StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(dataUri);
                    jsonText = await FileIO.ReadTextAsync(file);
                    MediaDataPath = "ms-appx:///DataModel/MediaData.json";
                }
            }
            else
            {
                if (path.StartsWith("ms-appx://"))
                {
                    Uri dataUri = new Uri(path);

                    StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(dataUri);
                    jsonText = await FileIO.ReadTextAsync(file);
                    MediaDataPath = path;
                }
                else if (path.StartsWith("https://"))
                {
                    try
                    {
                        using (var client = new HttpClient())
                        {
                            var response = await client.GetAsync(path);
                            response.EnsureSuccessStatusCode();
                            jsonText = await response.Content.ReadAsStringAsync();
                            MediaDataPath = path;
                        }
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("{0:d/M/yyyy HH:mm:ss.fff}", DateTime.Now) + " Exception while opening the playlist: " + path + " Exception: " + e.Message);
                    }
                }
                else if (path.StartsWith("http://"))
                {
                    try
                    {
                        //Download the json file from the server to configure what content will be dislayed.
                        //You can also modify the local MediaData.json file and delete this code block to test
                        //the local json file

                        Windows.Web.Http.Filters.HttpBaseProtocolFilter filter = new Windows.Web.Http.Filters.HttpBaseProtocolFilter();
                        filter.CacheControl.ReadBehavior = Windows.Web.Http.Filters.HttpCacheReadBehavior.MostRecent;
                        Windows.Web.Http.HttpClient http = new Windows.Web.Http.HttpClient(filter);
                        Uri httpUri = new Uri(path);
                        jsonText = await http.GetStringAsync(httpUri);
                        MediaDataPath = path;
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("{0:d/M/yyyy HH:mm:ss.fff}", DateTime.Now) + " Exception while opening the playlist: " + path + " Exception: " + e.Message);
                    }
                }
                else
                {
                    try
                    {
                        //Download the json file from the server to configure what content will be dislayed.
                        //You can also modify the local MediaData.json file and delete this code block to test
                        //the local json file
                        string MediaDataFile = path;
                        StorageFile file;
                        file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
                        if (file != null)
                        {
                            jsonText = await FileIO.ReadTextAsync(file);
                            MediaDataPath = MediaDataFile;
                        }
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("{0:d/M/yyyy HH:mm:ss.fff}", DateTime.Now) + " Exception while opening the playlist: " + path + " Exception: " + e.Message);
                    }
                }
            }

            if (string.IsNullOrEmpty(jsonText))
                return false;

            try
            {
                JsonObject jsonObject = JsonObject.Parse(jsonText);
                JsonArray jsonArray = jsonObject["Groups"].GetArray();

                foreach (JsonValue groupValue in jsonArray)
                {
                    JsonObject groupObject = groupValue.GetObject();
                    MediaDataGroup group = new MediaDataGroup(groupObject["UniqueId"].GetString(),
                                                                groupObject["Title"].GetString(),
                                                                groupObject["Category"].GetString(),
                                                                groupObject["ImagePath"].GetString(),
                                                                groupObject["Description"].GetString());

                    foreach (JsonValue itemValue in groupObject["Items"].GetArray())
                    {
                        JsonObject itemObject = itemValue.GetObject();
                        long timeValue = 0;
                        if (!itemObject.ContainsKey("Title"))
                        {
                            var id = itemObject["UniqueId"].GetString().GetHashCode().ToString();
                            var videoPath = _localVideoReady.ContainsKey(id) ? ApplicationData.Current.LocalFolder.Path + "/" + _localVideoReady[id] : itemObject["Content"].GetString();
                            group.Items.Add(new MediaItem(id, "", "", "ms-appx:///Assets/SMOOTH.png",
                                                               "", videoPath, "", 0, 0, "", "", "", false));
                        }
                        else
                        {
                            var id = itemObject["UniqueId"].GetString().GetHashCode().ToString();
                            var videoPath = _localVideoReady.ContainsKey(id) ? ApplicationData.Current.LocalFolder.Path + "/" + _localVideoReady[id] : itemObject["Content"].GetString();
                            group.Items.Add(new MediaItem(id,
                                                               itemObject["Comment"].GetString(),
                                                               itemObject["Title"].GetString(),
                                                               itemObject["ImagePath"].GetString(),
                                                               itemObject["Description"].GetString(),
                                                               videoPath,
                                                               itemObject["PosterContent"].GetString(),
                                                               (long.TryParse(itemObject["Start"].GetString(), out timeValue) ? timeValue : 0),
                                                               (long.TryParse(itemObject["Duration"].GetString(), out timeValue) ? timeValue : 0),
                                                               (itemObject.ContainsKey("HttpHeaders") ? itemObject["HttpHeaders"].GetString() : ""),
                                                               itemObject["PlayReadyUrl"].GetString(),
                                                               itemObject["PlayReadyCustomData"].GetString(),
                                                               itemObject["BackgroundAudio"].GetBoolean()));
                        }
                    }
                    if (Groups.Any(g => g.UniqueId == group.UniqueId))
                    {
                        var currentGroup = Groups.First(g => g.UniqueId == group.UniqueId);
                        currentGroup.Items.Where(item => group.Items.All(i => i.UniqueId != item.UniqueId) || item.Content.Contains("http")).ToList()
                            .ForEach(item => currentGroup.Items.Remove(item));
                        foreach (var item in group.Items)
                        {
                            if (currentGroup.Items.All(i => i.UniqueId != item.UniqueId))
                            {
                                currentGroup.Items.Add(item);
                            }
                        }
                    }
                    else
                    {
                        Groups.Add(group);
                    }

                    foreach (var item in group.Items)
                    {
                        if (!_localVideoReady.ContainsKey(item.UniqueId) &&
                            !_toDownload.Exists(x => x.Id == item.UniqueId))
                        {
                            _toDownload.Add(new Video { Id = item.UniqueId, Url = item.Content });
                        }
                    }
                    var itemReady = new List<Video>();
                    foreach (var item in _toDownload)
                    {
                        try
                        {
                            var fileName = string.Format(@"{0}.mp4", Guid.NewGuid());
                            // create the blank file in specified folder
                            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                            // create the downloader instance and prepare for downloading the file
                            var downloadOperation = new BackgroundDownloader().CreateDownload(new Uri(item.Url), file);
                            // start the download operation asynchronously
                            await downloadOperation.StartAsync();
                            _localVideoReady[item.Id] = fileName;
                            itemReady.Add(item);
                            await SaveVideos();
                        }
                        catch (Exception e)
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("{0:d/M/yyyy HH:mm:ss.fff}", DateTime.Now) + " Exception while Downloading: " + item.Url + " Exception: " + e.Message);
                        }
                    }
                    itemReady.ForEach(x => _toDownload.Remove(x));
                    return true;
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("{0:d/M/yyyy HH:mm:ss.fff}", DateTime.Now) + " Exception while opening the playlist: " + path + " Exception: " + e.Message);
            }
            return false;
        }
    }
}
