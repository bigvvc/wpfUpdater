﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.ComponentModel;
using System.IO;
using System.Xml;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Web;
using System.Globalization;
using System.Collections.Specialized;

namespace CsUpdater
{
  [XmlRoot("app")]
  public class UpdaterApp
  {
    [XmlElement("id")]
    public int Id
    {
      get;
      set;
    }

    [XmlAttribute("name")]
    public string Name
    {
      get;
      set;
    }

    [XmlAttribute("version")]
    public string VersionString
    {
      get;
      set;
    }

    [XmlIgnore]
    public Version Version
    {
      get
      {
        return new Version(VersionString);
      }
    }

    [XmlElement("changelog")]
    public string Changelog
    {
      get;
      set;
    }

    [XmlElement("message")]
    public string Message
    {
      get;
      set;
    }

    [XmlAttribute("releasedate")]
    public double ReleaseDateEpoch
    {
      get;
      set;
    }

    [XmlIgnore]
    public DateTime ReleaseDate
    {
      get
      {
        DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        epoch = epoch.AddSeconds(ReleaseDateEpoch);        
        return epoch.ToLocalTime();
      }
    }

    [XmlAttribute("filename")]
    public string FileName
    {
      get;
      set;
    }

    [XmlAttribute("url")]
    public string Url
    {
      get;
      set;
    }

    public static UpdaterApp Deserialize(XmlDocument xml)
    {
      if (xml == null)
        return null;

      try {
        XmlSerializer serializer = new XmlSerializer(typeof(UpdaterApp));

        UpdaterApp result;
        using (XmlReader r = new XmlNodeReader(xml.ChildNodes[1].FirstChild))
          result = serializer.Deserialize(r) as UpdaterApp;
        return result;
      }
      catch (Exception) {
        return null;
      }
    } // Deserialize
  }

  public class Updater
  {
    #region Delegates
    public delegate void CheckCompleted(UpdaterApp app);
    public delegate void Downloading(string filename, double percentage);
    public delegate void DownloadCompleted(string filename);
    public delegate void DownloadFailed(string filename, Exception exception);

    public CheckCompleted CheckCompletedDelegate;
    public Downloading DownloadingDelegate;
    public DownloadCompleted DownloadCompletedDelegate;
    public DownloadFailed DownloadFailedDelegate;
    #endregion

    bool m_Checking = false;
    string m_DownloadingFile = string.Empty;

    public Uri Url
    {
      get;
      set;
    }

    public string AppName
    {
      get;
      set;
    }

    public string Platform
    {
      get;
      set;
    }

    public string AppCurrentVersion
    {
      get;
      set;
    }

    public Updater(Uri url, string appName, string platform)
    {
      Url = url;
      AppName = appName;
      Platform = platform;
    }

    #region Public Operations
    public void Check()
    {
      if (m_Checking)
        return;

      System.Threading.ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback(CheckThread));
    }

    public void Download(string url, string filename)
    {
      if (!string.IsNullOrEmpty(m_DownloadingFile))
        return;

      string path = Path.GetDirectoryName(filename);
      if (!Directory.Exists(path)){
        try{
          Directory.CreateDirectory(path);
        }catch (Exception ex){
          if (DownloadFailedDelegate != null)
            DownloadFailedDelegate(m_DownloadingFile, ex);
          return;
        }
      }

      m_DownloadingFile = filename;
      WebClient webClient = new WebClient();
      webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadFileCompleted);
      webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressChanged);
      webClient.DownloadFileAsync(new Uri(url), filename);
    }

    public void OpenInBrowser(string url)
    {
      ProcessStartInfo psInfo = new ProcessStartInfo(url);
      psInfo.UseShellExecute = true;
      Process process = Process.Start(psInfo);
    }
    #endregion

    #region Private Operations
    private void CheckThread(object state)
    {
      m_Checking = true;
      UpdaterApp app = null;
      string url = string.Format("{0}?appname={1}&platform={2}&version={3}", Url.ToString(),
                                  Uri.EscapeDataString(AppName), Uri.EscapeDataString(Platform),
                                  string.IsNullOrEmpty(AppCurrentVersion) ? string.Empty : Uri.EscapeDataString(AppCurrentVersion));
      try {
        using (WebClient client = new WebClient()) {
          using (Stream data = client.OpenRead(url)) {
            StreamReader reader = new StreamReader(data);
            string str = null;
            StringBuilder sb = new StringBuilder();
            while ((str = reader.ReadLine()) != null)
              sb.AppendLine(str);

            string xml = sb.ToString();
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            app = UpdaterApp.Deserialize(doc);
          }
        }
      }
      catch (Exception) {

      }

      m_Checking = false;
      if (CheckCompletedDelegate != null)
        CheckCompletedDelegate(app);
    }

    private void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
      if (DownloadingDelegate != null)
        DownloadingDelegate(m_DownloadingFile, e.ProgressPercentage);
    }

    private void DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
    {
      bool error = e.Error != null;
      if (!error && DownloadCompletedDelegate != null)
        DownloadCompletedDelegate(m_DownloadingFile);
      else if (error && DownloadFailedDelegate != null)
        DownloadFailedDelegate(m_DownloadingFile, e.Error);
      m_DownloadingFile = string.Empty;
    }
    #endregion
  }

  public class BugReporter
  {
    public Uri Url
    {
      get;
      set;
    }

    public BugReporter(Uri url)
    {
      Url = url;
    }

    public void BugReport(string appName, string appVersion, string appPlatform, string email, string text)
    {
      Dictionary<string, string> parameters = new Dictionary<string, string>();
      parameters["appname"] = appName;
      parameters["platform"] = appPlatform;
      parameters["version"] = appVersion;
      parameters["email"] = email;
      parameters["text"] = text;

      using (WebClient client = new WebClient()) {
        NameValueCollection data = new NameValueCollection();
        foreach (string k in parameters.Keys)
          data[k] = parameters[k];

        try {
          byte[] response = client.UploadValues(Url, "POST", data);
          XmlDocument doc = new XmlDocument();
          doc.LoadXml(System.Text.Encoding.UTF8.GetString(response));
        }
        catch (Exception) {
          return;
        }
      }
    } // BugReport
  }
}
