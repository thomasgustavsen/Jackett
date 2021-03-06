﻿using CsQuery;
using Jackett.Models;
using Jackett.Services;
using Jackett.Utils;
using Jackett.Utils.Clients;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Jackett.Models.IndexerConfig;
using System.Collections.Specialized;

namespace Jackett.Indexers
{
    public class TorrentDay : BaseIndexer, IIndexer
    {
        private string UseLink { get { return (!String.IsNullOrEmpty(this.configData.AlternateLink.Value) ? this.configData.AlternateLink.Value : SiteLink); } }
        private string StartPageUrl { get { return UseLink + "login.php"; } }
        private string LoginUrl { get { return UseLink + "tak3login.php"; } }
        private string SearchUrl { get { return UseLink + "browse.php"; } }
        private List<String> KnownURLs = new List<String> {
            "https://tdonline.org/",
            "https://secure.torrentday.com/",
            "https://torrentday.eu/",
            "https://torrentday.it/",
            "https://classic.torrentday.com/",
            "https://www.torrentday.com/",
            "https://td-update.com/",
            "https://www.torrentday.me/",
            "https://www.torrentday.ru/",
            "https://www.torrentday.com/",
            "https://www.td.af/",
        };

        new ConfigurationDataRecaptchaLoginWithAlternateLink configData
        {
            get { return (ConfigurationDataRecaptchaLoginWithAlternateLink)base.configData; }
            set { base.configData = value; }
        }

        public TorrentDay(IIndexerManagerService i, Logger l, IWebClient wc, IProtectionService ps)
            : base(name: "TorrentDay",
                description: "TorrentDay",
                link: "https://torrentday.it/",
                caps: TorznabUtil.CreateDefaultTorznabTVCaps(),
                manager: i,
                client: wc,
                logger: l,
                p: ps,
                configData: new ConfigurationDataRecaptchaLoginWithAlternateLink())
        {
            Encoding = Encoding.GetEncoding("UTF-8");

            this.configData.Instructions.Value = this.DisplayName + " has multiple URLs. The default (" + this.SiteLink + ") can be changed by entering a new value in the box below.";
            this.configData.Instructions.Value += "The following are some known URLs for " + this.DisplayName;
            this.configData.Instructions.Value += "<ul><li>" + String.Join("</li><li>", this.KnownURLs.ToArray()) + "</li></ul>";

            AddCategoryMapping(29, TorznabCatType.TVAnime); // Anime
            AddCategoryMapping(28, TorznabCatType.PC); // Appz/Packs
            AddCategoryMapping(42, TorznabCatType.AudioAudiobook); // Audio Books
            AddCategoryMapping(20, TorznabCatType.Books); // Books
            AddCategoryMapping(30, TorznabCatType.TVDocumentary); // Documentary
            AddCategoryMapping(47, TorznabCatType.Other); // Fonts
            AddCategoryMapping(43, TorznabCatType.PCMac); // Mac

            AddCategoryMapping(25, TorznabCatType.MoviesSD); // Movies/480p
            AddCategoryMapping(11, TorznabCatType.MoviesBluRay); // Movies/Bluray
            AddCategoryMapping(5, TorznabCatType.MoviesBluRay); // Movies/Bluray-Full
            AddCategoryMapping(3, TorznabCatType.MoviesDVD); // Movies/DVD-R
            AddCategoryMapping(21, TorznabCatType.MoviesSD); // Movies/MP4
            AddCategoryMapping(22, TorznabCatType.MoviesForeign); // Movies/Non-English
            AddCategoryMapping(13, TorznabCatType.Movies); // Movies/Packs
            AddCategoryMapping(44, TorznabCatType.MoviesSD); // Movies/SD/x264
            AddCategoryMapping(48, TorznabCatType.MoviesHD); // Movies/x265
            AddCategoryMapping(1, TorznabCatType.MoviesSD); // Movies/XviD

            AddCategoryMapping(23, TorznabCatType.AudioForeign); // Music/Non-English
            AddCategoryMapping(41, TorznabCatType.Audio); // Music/Packs
            AddCategoryMapping(16, TorznabCatType.AudioVideo); // Music/Video
            AddCategoryMapping(45, TorznabCatType.AudioOther); // Podcast

            AddCategoryMapping(4, TorznabCatType.PCGames); // PC/Games
            AddCategoryMapping(18, TorznabCatType.ConsolePS3); // PS3
            AddCategoryMapping(8, TorznabCatType.ConsolePSP); // PSP
            AddCategoryMapping(10, TorznabCatType.ConsoleWii); // Wii
            AddCategoryMapping(9, TorznabCatType.ConsoleXbox360); // Xbox-360

            AddCategoryMapping(24, TorznabCatType.TVSD); // TV/480p
            AddCategoryMapping(32, TorznabCatType.TVHD); // TV/Bluray
            AddCategoryMapping(31, TorznabCatType.TVSD); // TV/DVD-R
            AddCategoryMapping(33, TorznabCatType.TVSD); // TV/DVD-Rip
            AddCategoryMapping(46, TorznabCatType.TVSD); // TV/Mobile
            AddCategoryMapping(14, TorznabCatType.TV); // TV/Packs
            AddCategoryMapping(26, TorznabCatType.TVSD); // TV/SD/x264
            AddCategoryMapping(7, TorznabCatType.TVHD); // TV/x264
            AddCategoryMapping(34, TorznabCatType.TVHD); // TV/x265
            AddCategoryMapping(2, TorznabCatType.TVSD); // TV/XviD

            AddCategoryMapping(6, TorznabCatType.XXX); // XXX/Movies
            AddCategoryMapping(15, TorznabCatType.XXXPacks); // XXX/Packs
        }

        public override async Task<ConfigurationData> GetConfigurationForSetup()
        {
            var loginPage = await RequestStringWithCookies(StartPageUrl, string.Empty);
            CQ cq = loginPage.Content;
            var result = this.configData;
            result.CookieHeader.Value = loginPage.Cookies;
            result.Captcha.SiteKey = cq.Find(".g-recaptcha").Attr("data-sitekey");
            result.Captcha.Version = "2";
            return result;
        }

        public async Task<IndexerConfigurationStatus> ApplyConfiguration(JToken configJson)
        {
            configData.LoadValuesFromJson(configJson);
            var pairs = new Dictionary<string, string> {
                { "username", configData.Username.Value },
                { "password", configData.Password.Value },
                { "g-recaptcha-response", configData.Captcha.Value }
            };

            if (!string.IsNullOrWhiteSpace(configData.Captcha.Cookie))
            {
                // Cookie was manually supplied
                CookieHeader = configData.Captcha.Cookie;
                try
                {
                    var results = await PerformQuery(new TorznabQuery());
                    if (results.Count() == 0)
                    {
                        throw new Exception("Your cookie did not work");
                    }

                    SaveConfig();
                    IsConfigured = true;
                    return IndexerConfigurationStatus.Completed;
                }
                catch (Exception e)
                {
                    IsConfigured = false;
                    throw new Exception("Your cookie did not work: " + e.Message);
                }
            }

            var result = await RequestLoginAndFollowRedirect(LoginUrl, pairs, configData.CookieHeader.Value, true, UseLink, LoginUrl);
            await ConfigureIfOK(result.Cookies, result.Content != null && result.Content.Contains("logout.php"), () =>
            {
                CQ dom = result.Content;
                var messageEl = dom["#login"];
                messageEl.Children("form").Remove();
                var errorMessage = messageEl.Text().Trim();

                if (string.IsNullOrWhiteSpace(errorMessage))
                {
                    errorMessage = dom.Text();
                }

                throw new ExceptionWithConfigData(errorMessage, configData);
            });
            return IndexerConfigurationStatus.RequiresTesting;
        }

        public async Task<IEnumerable<ReleaseInfo>> PerformQuery(TorznabQuery query)
        {
            var releases = new List<ReleaseInfo>();
            var searchString = query.GetQueryString();
            var queryUrl = SearchUrl;
            var queryCollection = new NameValueCollection();

            if (query.QueryType == "TorrentPotato" && !string.IsNullOrWhiteSpace(query.ImdbID) && query.ImdbID.ToLower().StartsWith("tt"))
            {
                queryCollection.Add("search", query.ImdbID);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(searchString))
                    queryCollection.Add("search", searchString);
            }

            foreach (var cat in MapTorznabCapsToTrackers(query))
                queryCollection.Add("c" + cat, "1");

            if (queryCollection.Count > 0)
                queryUrl += "?" + queryCollection.GetQueryString();

            var results = await RequestStringWithCookiesAndRetry(queryUrl);

            // Check for being logged out
            if (results.IsRedirect)
                throw new AuthenticationException();

            try
            {
                CQ dom = results.Content;
                var rows = dom["#torrentTable > tbody > tr.browse"];
                foreach (var row in rows)
                {
                    CQ qRow = row.Cq();
                    var release = new ReleaseInfo();

                    release.MinimumRatio = 1;
                    release.MinimumSeedTime = 172800;
                    release.Title = qRow.Find(".torrentName").Text();
                    release.Description = release.Title;
                    release.Guid = new Uri(UseLink + qRow.Find(".torrentName").Attr("href"));
                    release.Comments = release.Guid;
                    release.Link = new Uri(UseLink + qRow.Find(".dlLinksInfo > a").Attr("href"));

                    var sizeStr = qRow.Find(".sizeInfo").Text();
                    release.Size = ReleaseInfo.GetBytes(sizeStr);

                    var dateStr = qRow.Find(".ulInfo").Text().Split('|').Last().Trim();
                    var agoIdx = dateStr.IndexOf("ago");
                    if (agoIdx > -1)
                    {
                        dateStr = dateStr.Substring(0, agoIdx);
                    }
                    release.PublishDate = DateTimeUtil.FromTimeAgo(dateStr);

                    release.Seeders = ParseUtil.CoerceInt(qRow.Find(".seedersInfo").Text());
                    release.Peers = ParseUtil.CoerceInt(qRow.Find(".leechersInfo").Text()) + release.Seeders;

                    var cat = qRow.Find("td:eq(0) a").First().Attr("href").Split('#')[0].Substring(15);//browse.php?cat=24
                    release.Category = MapTrackerCatToNewznab(cat);

                    if (qRow.Find("span.flTags").Length >= 1)
                        release.DownloadVolumeFactor = 0;
                    else
                        release.DownloadVolumeFactor = 1;

                    release.UploadVolumeFactor = 1;

                    releases.Add(release);
                }
            }
            catch (Exception ex)
            {
                OnParseError(results.Content, ex);
            }
            return releases;
        }
    }
}
