using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;
using AngleSharp.Html.Parser;
using FluentValidation;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers.Settings;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.IndexerVersions;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.Definitions
{
    public class GazelleGames : TorrentIndexerBase<GazelleGamesSettings>
    {
        public override string Name => "GazelleGames";
        public override DownloadProtocol Protocol => DownloadProtocol.Torrent;

        public GazelleGames(IIndexerHttpClient httpClient, IEventAggregator eventAggregator, IIndexerStatusService indexerStatusService, IIndexerDefinitionUpdateService definitionService, IConfigService configService, Logger logger)
            : base(httpClient, eventAggregator, indexerStatusService, definitionService, configService, logger)
        {
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            return new GazelleGamesRequestGenerator() { Settings = Settings, Capabilities = Capabilities };
        }

        public override IParseIndexerResponse GetParser()
        {
            return new GazelleGamesParser(Settings, Capabilities.Categories);
        }

        protected override IDictionary<string, string> GetCookies()
        {
            return CookieUtil.CookieHeaderToDictionary(Settings.Cookie);
        }

        protected override bool CheckIfLoginNeeded(HttpResponse httpResponse)
        {
            if (httpResponse.HasHttpRedirect && httpResponse.RedirectUrl.EndsWith("login.php"))
            {
                return true;
            }

            return false;
        }
    }

    public class GazelleGamesRequestGenerator : IIndexerRequestGenerator
    {
        public GazelleGamesSettings Settings { get; set; }
        public IndexerCapabilities Capabilities { get; set; }

        public GazelleGamesRequestGenerator()
        {
        }

        private IEnumerable<IndexerRequest> GetPagedRequests(string term, int[] categories)
        {
            var searchUrl = string.Format("{0}/torrents.php", Settings.BaseUrl.TrimEnd('/'));

            var searchString = term;

            var searchType = Settings.SearchGroupNames ? "groupname" : "searchstr";

            var queryCollection = new NameValueCollection
            {
                { searchType, searchString },
                { "order_by", "time" },
                { "order_way", "desc" },
                { "action", "basic" },
                { "searchsubmit", "1" }
            };

            var i = 0;

            foreach (var cat in Capabilities.Categories.MapTorznabCapsToTrackers(categories))
            {
                queryCollection.Add($"artistcheck[{i++}]", cat);
            }

            searchUrl += "?" + queryCollection.GetQueryString();

            var request = new IndexerRequest(searchUrl, HttpAccept.Html);

            yield return request;
        }

        public IndexerPageableRequestChain GetSearchRequests(MovieSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();

            pageableRequests.Add(GetPagedRequests(string.Format("{0}", searchCriteria.SanitizedSearchTerm), searchCriteria.Categories));

            return pageableRequests;
        }

        public IndexerPageableRequestChain GetSearchRequests(MusicSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();

            pageableRequests.Add(GetPagedRequests(string.Format("{0}", searchCriteria.SanitizedSearchTerm), searchCriteria.Categories));

            return pageableRequests;
        }

        public IndexerPageableRequestChain GetSearchRequests(TvSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();

            pageableRequests.Add(GetPagedRequests(string.Format("{0}", searchCriteria.SanitizedTvSearchString), searchCriteria.Categories));

            return pageableRequests;
        }

        public IndexerPageableRequestChain GetSearchRequests(BookSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();

            pageableRequests.Add(GetPagedRequests(string.Format("{0}", searchCriteria.SanitizedSearchTerm), searchCriteria.Categories));

            return pageableRequests;
        }

        public IndexerPageableRequestChain GetSearchRequests(BasicSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();

            pageableRequests.Add(GetPagedRequests(string.Format("{0}", searchCriteria.SanitizedSearchTerm), searchCriteria.Categories));

            return pageableRequests;
        }

        public Func<IDictionary<string, string>> GetCookies { get; set; }
        public Action<IDictionary<string, string>, DateTime?> CookiesUpdater { get; set; }
    }

    public class GazelleGamesParser : IParseIndexerResponse
    {
        private readonly GazelleGamesSettings _settings;
        private readonly IndexerCapabilitiesCategories _categories;

        public GazelleGamesParser(GazelleGamesSettings settings, IndexerCapabilitiesCategories categories)
        {
            _settings = settings;
            _categories = categories;
        }

        public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
        {
            var torrentInfos = new List<ReleaseInfo>();

            var rowsSelector = ".torrent_table > tbody > tr";

            var searchResultParser = new HtmlParser();
            var searchResultDocument = searchResultParser.ParseDocument(indexerResponse.Content);
            var rows = searchResultDocument.QuerySelectorAll(rowsSelector);

            var stickyGroup = false;
            string categoryStr;
            ICollection<IndexerCategory> groupCategory = null;
            string groupTitle = null;

            foreach (var row in rows)
            {
                if (row.ClassList.Contains("torrent"))
                {
                    // garbage rows
                    continue;
                }
                else if (row.ClassList.Contains("group"))
                {
                    stickyGroup = row.ClassList.Contains("sticky");
                    var dispalyname = row.QuerySelector("#displayname");
                    var qCat = row.QuerySelector("td.cats_col > div");
                    categoryStr = qCat.GetAttribute("title");
                    var qArtistLink = dispalyname.QuerySelector("#groupplatform > a");
                    if (qArtistLink != null)
                    {
                        categoryStr = ParseUtil.GetArgumentFromQueryString(qArtistLink.GetAttribute("href"), "artistname");
                    }

                    groupCategory = _categories.MapTrackerCatToNewznab(categoryStr);

                    var qDetailsLink = dispalyname.QuerySelector("#groupname > a");
                    groupTitle = qDetailsLink.TextContent;
                }
                else if (row.ClassList.Contains("group_torrent"))
                {
                    if (row.QuerySelector("td.edition_info") != null)
                    {
                        continue;
                    }

                    var sizeString = row.QuerySelector("td:nth-child(4)").TextContent;
                    if (string.IsNullOrEmpty(sizeString))
                    {
                        continue;
                    }

                    var qDetailsLink = row.QuerySelector("a[href^=\"torrents.php?id=\"]");
                    var title = qDetailsLink.TextContent.Replace(", Freeleech!", "").Replace(", Neutral Leech!", "");

                    //if (stickyGroup && (query.ImdbID == null || !NewznabStandardCategory.MovieSearchImdbAvailable) && !query.MatchQueryStringAND(title)) // AND match for sticky releases
                    //{
                    //    continue;
                    //}
                    var qDescription = qDetailsLink.QuerySelector("span.torrent_info_tags");
                    var qDLLink = row.QuerySelector("a[href^=\"torrents.php?action=download\"]");
                    var qTime = row.QuerySelector("span.time");
                    var qGrabs = row.QuerySelector("td:nth-child(5)");
                    var qSeeders = row.QuerySelector("td:nth-child(6)");
                    var qLeechers = row.QuerySelector("td:nth-child(7)");
                    var qFreeLeech = row.QuerySelector("strong.freeleech_label");
                    var qNeutralLeech = row.QuerySelector("strong.neutralleech_label");
                    var time = qTime.GetAttribute("title");
                    var link = _settings.BaseUrl + qDLLink.GetAttribute("href");
                    var seeders = ParseUtil.CoerceInt(qSeeders.TextContent);
                    var publishDate = DateTime.SpecifyKind(
                        DateTime.ParseExact(time, "MMM dd yyyy, HH:mm", CultureInfo.InvariantCulture),
                        DateTimeKind.Unspecified).ToLocalTime();
                    var details = _settings.BaseUrl + qDetailsLink.GetAttribute("href");
                    var grabs = ParseUtil.CoerceInt(qGrabs.TextContent);
                    var leechers = ParseUtil.CoerceInt(qLeechers.TextContent);
                    var size = ParseUtil.GetBytes(sizeString);

                    var release = new TorrentInfo
                    {
                        MinimumRatio = 1,
                        MinimumSeedTime = 288000, //80 hours
                        Categories = groupCategory,
                        PublishDate = publishDate,
                        Size = size,
                        InfoUrl = details,
                        DownloadUrl = link,
                        Guid = link,
                        Grabs = grabs,
                        Seeders = seeders,
                        Peers = leechers + seeders,
                        Title = title,
                        Description = qDescription?.TextContent,
                        UploadVolumeFactor = qNeutralLeech is null ? 1 : 0,
                        DownloadVolumeFactor = qFreeLeech != null || qNeutralLeech != null ? 0 : 1
                    };

                    torrentInfos.Add(release);
                }
            }

            return torrentInfos.ToArray();
        }

        public Action<IDictionary<string, string>, DateTime?> CookiesUpdater { get; set; }
    }

    public class GazelleGamesSettings : CookieTorrentBaseSettings
    {
        public GazelleGamesSettings()
        {
            SearchGroupNames = false;
        }

        [FieldDefinition(3, Label = "Search Group Names", Type = FieldType.Checkbox, HelpText = "Search Group Names Only")]
        public bool SearchGroupNames { get; set; }
    }
}
