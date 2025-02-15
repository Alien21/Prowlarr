using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Indexers.Settings;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.Definitions
{
    [Obsolete("Site unusable due to lack of new releases")]
    public class Shizaproject : TorrentIndexerBase<NoAuthTorrentBaseSettings>
    {
        public override string Name => "ShizaProject";
        public override string[] IndexerUrls => new[] { "https://shiza-project.com/" };
        public override string Description => "ShizaProject Tracker is a Public RUSSIAN tracker and release group for ANIME";
        public override string Language => "ru-RU";
        public override Encoding Encoding => Encoding.UTF8;
        public override IndexerPrivacy Privacy => IndexerPrivacy.Public;
        public override IndexerCapabilities Capabilities => SetCapabilities();

        public Shizaproject(IIndexerHttpClient httpClient, IEventAggregator eventAggregator, IIndexerStatusService indexerStatusService, IConfigService configService, Logger logger)
            : base(httpClient, eventAggregator, indexerStatusService, configService, logger)
        {
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            return new ShizaprojectRequestGenerator { Settings = Settings, Capabilities = Capabilities };
        }

        public override IParseIndexerResponse GetParser()
        {
            return new ShizaprojectParser(Settings, Capabilities.Categories);
        }

        private IndexerCapabilities SetCapabilities()
        {
            var caps = new IndexerCapabilities
            {
                TvSearchParams = new List<TvSearchParam>
                {
                    TvSearchParam.Q, TvSearchParam.Season, TvSearchParam.Ep
                },
                MovieSearchParams = new List<MovieSearchParam>
                {
                    MovieSearchParam.Q
                }
            };

            caps.Categories.AddCategoryMapping(1, NewznabStandardCategory.TVAnime, "TV");
            caps.Categories.AddCategoryMapping(2, NewznabStandardCategory.TVAnime, "TV_SPECIAL");
            caps.Categories.AddCategoryMapping(3, NewznabStandardCategory.TVAnime, "ONA");
            caps.Categories.AddCategoryMapping(4, NewznabStandardCategory.TVAnime, "OVA");
            caps.Categories.AddCategoryMapping(5, NewznabStandardCategory.Movies, "MOVIE");
            caps.Categories.AddCategoryMapping(6, NewznabStandardCategory.Movies, "SHORT_MOVIE");

            return caps;
        }
    }

    public class ShizaprojectRequestGenerator : IIndexerRequestGenerator
    {
        public NoAuthTorrentBaseSettings Settings { get; set; }
        public IndexerCapabilities Capabilities { get; set; }

        private IEnumerable<IndexerRequest> GetPagedRequests(string term, int[] categories)
        {
            var variables = new
            {
                // Number of fetched releases (required parameter) TODO: consider adding pagination
                first = 50,

                // Remove season and episode info from search term cause it breaks search
                query = Regex.Replace(term, @"(?:[SsEe]?\d{1,4}){1,2}$", "").TrimEnd()
            };

            var query = @"
            query fetchReleases($first: Int, $query: String) {
                releases(first: $first, query: $query) {
                    edges {
                        node {
                            name
                            type
                            originalName
                            alternativeNames
                            publishedAt
                            slug
                            torrents {
                                synopsis
                                downloaded
                                seeders
                                leechers
                                size
                                magnetUri
                                updatedAt
                                file {
                                    url
                                }
                                videoQualities
                            }
                        }
                    }
                }
            }";

            var queryCollection = new NameValueCollection
            {
                { "query", query.Replace('\n', ' ').Trim() },
                { "variables", JsonConvert.SerializeObject(variables) }
            };

            var requestUrl = string.Format("{0}/graphql?", Settings.BaseUrl.TrimEnd('/')) + queryCollection.GetQueryString();

            var request = new IndexerRequest(requestUrl, HttpAccept.Json);
            yield return request;
        }

        public IndexerPageableRequestChain GetSearchRequests(MovieSearchCriteria searchCriteria)
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

        public IndexerPageableRequestChain GetSearchRequests(BasicSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();

            pageableRequests.Add(GetPagedRequests(string.Format("{0}", searchCriteria.SanitizedSearchTerm), searchCriteria.Categories));

            return pageableRequests;
        }

        // Shizaproject doesn't support music, but this function required by interface
        public IndexerPageableRequestChain GetSearchRequests(MusicSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        // Shizaproject doesn't support books, but this function required by interface
        public IndexerPageableRequestChain GetSearchRequests(BookSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        public Func<IDictionary<string, string>> GetCookies { get; set; }
        public Action<IDictionary<string, string>, DateTime?> CookiesUpdater { get; set; }
    }

    public class ShizaprojectParser : IParseIndexerResponse
    {
        private readonly NoAuthTorrentBaseSettings _settings;
        private readonly IndexerCapabilitiesCategories _categories;

        public ShizaprojectParser(NoAuthTorrentBaseSettings settings, IndexerCapabilitiesCategories categories)
        {
            _settings = settings;
            _categories = categories;
        }

        private string ComposeTitle(ShizaprojectNode n, ShizaprojectTorrent tr)
        {
            var allNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                n.Name,
                n.OriginalName
            };
            allNames.UnionWith(n.AlternativeNames.ToHashSet());

            var title = $"{string.Join(" / ", allNames)} {tr.Synopsis}";

            if (tr.VideoQualities.Length > 0)
            {
                title += $" [{string.Join(" ", tr.VideoQualities)}]";
            }

            return title;
        }

        private DateTime GetActualPublishDate(ShizaprojectNode n, ShizaprojectTorrent t)
        {
            if (n.PublishedAt == null)
            {
                return t.UpdatedAt;
            }
            else
            {
                return (t.UpdatedAt > n.PublishedAt) ? t.UpdatedAt : n.PublishedAt.Value;
            }
        }

        private string GetResolution(string[] qualities)
        {
            var resPrefix = "RESOLUTION_";
            var res = Array.Find(qualities, s => s.StartsWith(resPrefix));
            return res != null ? res.Replace(resPrefix, "") : "Unknown";
        }

        public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
        {
            var torrentInfos = new List<ReleaseInfo>();
            var response = JsonConvert.DeserializeObject<ShizaprojectReleasesResponse>(indexerResponse.Content);

            if (response.Errors != null && response.Errors.Length > 0)
            {
                var message = "Errors:\n";
                foreach (var e in response.Errors)
                {
                    message += string.Format("{0} - {1}\n", e.Extensions.Code, e.Message);
                }

                throw new IndexerException(indexerResponse, message);
            }

            foreach (var e in response.Data.Releases.Edges)
            {
                foreach (var tr in e.Node.Torrents)
                {
                    var torrentInfo = new TorrentInfo
                    {
                        Title = ComposeTitle(e.Node, tr),
                        InfoUrl = string.Format("{0}/releases/{1}/", _settings.BaseUrl.TrimEnd('/'), e.Node.Slug),
                        DownloadVolumeFactor = 0,
                        UploadVolumeFactor = 1,
                        Seeders = tr.Seeders,
                        Peers = tr.Leechers + tr.Seeders,
                        Grabs = tr.Downloaded,
                        Categories = _categories.MapTrackerCatDescToNewznab(e.Node.Type),
                        PublishDate = GetActualPublishDate(e.Node, tr),
                        Guid = tr.File.Url,
                        DownloadUrl = tr.File.Url,
                        MagnetUrl = tr.MagnetUri,
                        Size = tr.Size,
                        Resolution = GetResolution(tr.VideoQualities)
                    };

                    torrentInfos.Add(torrentInfo);
                }
            }

            return torrentInfos.ToArray();
        }

        public Action<IDictionary<string, string>, DateTime?> CookiesUpdater { get; set; }
    }

    public class ShizaprojectReleasesResponse
    {
        public ShizaprojectData Data { get; set; }
        public ShizaprojectError[] Errors { get; set; }
    }

    public class ShizaprojectError
    {
        public ShizaprojectErrorExtensions Extensions { get; set; }
        public string Message { get; set; }
    }

    public class ShizaprojectErrorExtensions
    {
        public string Code { get; set; }
    }

    public class ShizaprojectData
    {
        public ShizaprojectReleases Releases { get; set; }
    }

    public class ShizaprojectReleases
    {
        public ShizaprojectEdge[] Edges { get; set; }
    }

    public class ShizaprojectEdge
    {
        public ShizaprojectNode Node { get; set; }
    }

    public class ShizaprojectNode
    {
        public string Name { get; set; }
        public string OriginalName { get; set; }
        public string[] AlternativeNames { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string Slug { get; set; }
        public ShizaprojectTorrent[] Torrents { get; set; }
        public string Type { get; set; }
    }

    public class ShizaprojectFile
    {
        public string Url { get; set; }
    }

    public class ShizaprojectTorrent
    {
        public string Synopsis { get; set; }
        public int Downloaded { get; set; }
        public int Seeders { get; set; }
        public int Leechers { get; set; }
        public long Size { get; set; }
        public string MagnetUri { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string[] VideoQualities { get; set; }
        public ShizaprojectFile File { get; set; }
    }
}
