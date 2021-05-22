using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Kinopoisk
{
    public static class ApiModelExtensions
    {
        public static RemoteSearchResult ToRemoteSearchResult(this Film src)
        {
            if (src?.Data is null)
                return null;

            var res = new RemoteSearchResult() {
                Name = src.GetLocalName(),
                ImageUrl = src.Data.PosterUrl,
                PremiereDate = src.Data.GetPremiereDate(),
                Overview = src.Data.Description,
                SearchProviderName = Utils.ProviderName
            };
            res.SetProviderId(Utils.ProviderId, Convert.ToString(src.Data.FilmId));

            return res;
        }

        public static IEnumerable<RemoteSearchResult> ToRemoteSearchResults(this FilmSearchResponse src)
        {
            if (src?.Films is null)
                return Enumerable.Empty<RemoteSearchResult>();

            return src.Films.Select(s => s.ToRemoteSearchResult());
        }

        public static RemoteSearchResult ToRemoteSearchResult(this FilmSearchResponse_films src)
        {
            if (src is null)
                return null;

            var res = new RemoteSearchResult() {
                Name = src.GetLocalName(),
                ImageUrl = src.PosterUrl,
                PremiereDate = src.GetPremiereDate(),
                Overview = src.Description,
                SearchProviderName = Utils.ProviderName
            };
            res.SetProviderId(Utils.ProviderId, Convert.ToString(src.FilmId));

            return res;
        }

        public static Series ToSeries(this Film src)
        {
            if (src?.Data is null)
                return null;

            var res = new Series();

            FillCommonFilmInfo(src, res);

            res.EndDate = src.Data.GetEndDate();
            res.Status = src.Data.IsContinuing()
                ? SeriesStatus.Continuing
                : SeriesStatus.Ended;

            return res;
        }

        public static Movie ToMovie(this Film src)
        {
            if (src?.Data is null)
                return null;

            var res = new Movie();

            FillCommonFilmInfo(src, res);

            return res;
        }

        private static void FillCommonFilmInfo(Film src, BaseItem dst)
        {
            dst.SetProviderId(Utils.ProviderId, Convert.ToString(src.Data.FilmId));
            dst.Name = src.GetLocalName();
            dst.OriginalTitle = src.GetOriginalNameIfNotSame();
            dst.PremiereDate = src.Data.GetPremiereDate();
            if (!string.IsNullOrWhiteSpace(src.Data.Slogan))
                dst.Tagline = src.Data.Slogan;
            dst.Overview = src.Data.Description;
            if (src.Data.Countries != null)
                dst.ProductionLocations = src.Data.Countries.Select(c => c.Country1).ToArray();
            if (src.Data.Genres != null)
                foreach(var genre in src.Data.Genres.Select(c => c.Genre1))
                    dst.AddGenre(genre);
            if (src.Data.RatingAgeLimits > 0)
                dst.OfficialRating = $"{src.Data.RatingAgeLimits}+";
            else
                dst.OfficialRating = src.Data.RatingMpaa;

            if (src.Rating != null)
            {
                var communityRating = src.Rating.Rating1 > 0
                    ? src.Rating.Rating1
                    : src.Rating.RatingImdb;
                dst.CommunityRating = communityRating > 0 ? (float)communityRating : null;
                dst.CriticRating = src.Rating.GetCriticRatingAsTenPointBased();
            }

            if (src.ExternalId != null)
            {
                if (!string.IsNullOrWhiteSpace(src.ExternalId.ImdbId))
                    dst.SetProviderId(MetadataProvider.Imdb, src.ExternalId.ImdbId);
            }
        }

        public static float? GetCriticRatingAsTenPointBased(this Rating src)
        {
            if (src is null)
                return null;

            if (string.IsNullOrWhiteSpace(src.RatingFilmCritics))
                return null;

            if (float.TryParse(src.RatingFilmCritics, out var res))
                return res;

            var ratingStr = src.RatingFilmCritics.Replace("%", string.Empty);
            if (int.TryParse(ratingStr, out var res_pct))
                return res_pct * 0.1f;

            return null;
        }

        public static IEnumerable<RemoteImageInfo> ToRemoteImageInfos(this Film src)
        {
            var res = Enumerable.Empty<RemoteImageInfo>();
            if (src is null)
                return res;

            if (src?.Data?.PosterUrl != null)
            {
                var mainPoster = new RemoteImageInfo(){
                    Type = ImageType.Primary,
                    Url = src.Data.PosterUrl,
                    Language = Utils.ProviderMetadataLanguage,
                    ProviderName = Utils.ProviderName
                };
                res = res.Concat(Enumerable.Repeat(mainPoster, 1));
            }

            if (src.Images != null)
            {
                if (src.Images.Posters != null)
                    res = res.Concat(src.Images.Posters.ToRemoteImageInfos(ImageType.Primary));
                if  (src.Images.Backdrops != null)
                    res = res.Concat(src.Images.Backdrops.ToRemoteImageInfos(ImageType.Backdrop));
            }

            return res;
        }

        public static IEnumerable<RemoteImageInfo> ToRemoteImageInfos(this IEnumerable<Images_posters> src, ImageType imageType)
        {
            return src.Select(s => s.ToRemoteImageInfo(imageType))
                .Where(s => s != null);
        }

        public static RemoteImageInfo ToRemoteImageInfo(this Images_posters src, ImageType imageType)
        {
            if (src is null)
                return null;

            return new RemoteImageInfo(){
                Type = imageType,
                Url = src.Url,
                Language = src.Language,
                Height = src.Height,
                Width = src.Width,
                ProviderName = Utils.ProviderName
            };
        }

        public static IReadOnlyList<MediaUrl> ToMediaUrls(this VideoResponse src)
        {
            if (src is null || src.Trailers is null || src.Trailers.Count < 1)
                return null;

            return src.Trailers.Select(t => t.ToMediaUrl())
                .Where(mu => mu != null)
                .ToList();
        }

        public static MediaUrl ToMediaUrl(this VideoResponse_trailers src) {
            if (src is null)
                return null;

            return new MediaUrl
            {
                Name = src.Name,
                Url = src.Url
            };
        }

        public static RemoteImageInfo ToRemoteImageInfo(this PersonResponse src)
        {
            if (src is null || string.IsNullOrEmpty(src.PosterUrl))
                return null;

            return new RemoteImageInfo(){
                Type = ImageType.Primary,
                Url = src.PosterUrl,
                ProviderName = Utils.ProviderName
            };
        }

        public static PersonInfo ToPersonInfo(this StaffResponse src)
        {
            if (src is null)
                return null;

            var res = new PersonInfo()
            {
                Name = src.NameRu,
                ImageUrl = src.PosterUrl,
                Role = src.ProfessionText ?? string.Empty,
                Type = src.ProfessionKey.ToPersonType()
            };
            if (string.IsNullOrWhiteSpace(res.Name))
                res.Name = src.NameEn ?? string.Empty;
            res.SetProviderId(Utils.ProviderId, Convert.ToString(src.StaffId));

            return res;
        }

        public static IEnumerable<PersonInfo> ToPersonInfos(this ICollection<StaffResponse> src)
        {
            var res = src.Select(s => s.ToPersonInfo())
                .Where(s => s != null)
                .ToArray();

            var i = 0;
            foreach(var item in res)
                item.SortOrder = ++i;

            return res;
        }

        public static string ToPersonType(this StaffResponseProfessionKey src)
        {
            return src switch
            {
                StaffResponseProfessionKey.ACTOR => PersonType.Actor,
                StaffResponseProfessionKey.DIRECTOR => PersonType.Director,
                StaffResponseProfessionKey.WRITER => PersonType.Writer,
                StaffResponseProfessionKey.COMPOSER => PersonType.Composer,
                StaffResponseProfessionKey.PRODUCER or StaffResponseProfessionKey.PRODUCER_USSR => PersonType.Producer,
                _ => string.Empty,
            };
        }

        public static DateTime? ParseDate(this string src){
            if (src == null)
                return null;

            if (DateTime.TryParseExact(src, "o", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var res))
                return res;

            return null;
        }

        public static DateTime? GetPremiereDate(this CommonFilmData src)
        {
            var res = src.IsRussianSpokenOriginated()
                ? src.PremiereRu.ParseDate()
                : src.PremiereWorld.ParseDate();
            if (src.PremiereRu.ParseDate() < res)
                res = src.PremiereRu.ParseDate();
            if (src.PremiereWorld.ParseDate() < res)
                res = src.PremiereWorld.ParseDate();
            if (src.PremiereDigital.ParseDate() < res)
                res = src.PremiereDigital.ParseDate();
            if (src.PremiereDvd.ParseDate() < res)
                res = src.PremiereDvd.ParseDate();
            if (src.PremiereBluRay.ParseDate() < res)
                res = src.PremiereBluRay.ParseDate();

            if (res.HasValue)
                return res;

            var firstYear = GetFirstYear(src.Year);
            if (firstYear != null)
                return new DateTime(firstYear.Value, 1, 1);

            return null;
        }

        public static DateTime? GetPremiereDate(this FilmSearchResponse_films src)
        {
            var firstYear = GetFirstYear(src.Year);
            if (firstYear != null)
                return new DateTime(firstYear.Value, 1, 1);

            return null;
        }

        public static DateTime? GetEndDate(this CommonFilmData src)
        {
            var lastYear = GetLastYear(src.Year);
            if (lastYear != null)
                return new DateTime(lastYear.Value, 12, 31);

            return null;
        }

        public static bool IsContinuing(this CommonFilmData src)
            => IsСontinuing(src?.Year);

        public static string GetLocalName(this Film src)
        {
            var res = src?.Data?.NameRu;
            if (string.IsNullOrWhiteSpace(res))
                res = src?.Data?.NameEn;
            return res;
        }

        public static string GetLocalName(this FilmSearchResponse_films src)
        {
            var res = src?.NameRu;
            if (string.IsNullOrWhiteSpace(res))
                res = src?.NameEn;
            return res;
        }

        public static string GetOriginalName(this Film src)
            => src.IsRussianSpokenOriginated()
                ? src?.Data?.NameRu
                : src?.Data?.NameEn;

        public static string GetOriginalNameIfNotSame(this Film src)
        {
            var localName = src.GetLocalName();
            var originalName = src.GetOriginalName();
            if (!string.IsNullOrWhiteSpace(originalName) && !string.Equals(localName, originalName))
                return originalName;

            return string.Empty;
        }

        public static bool IsRussianSpokenOriginated(this Film src)
            => src?.Data?.IsRussianSpokenOriginated() ?? false;

        public static bool IsRussianSpokenOriginated(this CommonFilmData src)
            => src?.Countries?.IsRussianSpokenOriginated() ?? false;

        public static bool IsRussianSpokenOriginated(this IEnumerable<Country> src)
        {
            if (src is null)
                return false;

            foreach(var country in src)
                switch(country.Country1)
                {
                    case "Россия":
                        return true;
                }

            return false;
        }

        public static int? GetFirstYear(string years)
        {
            if (string.IsNullOrWhiteSpace(years))
                return null;

            years = years.Trim();

            if (int.TryParse(years, out var res))
                return res;

            var i = 0;
            while (true) {
                if (i > 4)
                    return null;
                if (!char.IsDigit(years[i]))
                    break;
                i++;
            }

            return Convert.ToInt32(years.Substring(0, i));
        }

        public static bool IsСontinuing(string years)
            => years?.EndsWith("-...") ?? false;

        public static int? GetLastYear(string years)
        {
            if (string.IsNullOrWhiteSpace(years))
                return null;

            years = years.Trim();

            if (int.TryParse(years, out var res))
                return res;

            var i = 0;
            int startindex() => years.Length - 1 - i;
            while (true) {
                if (i > 4)
                    return null;
                if (!char.IsDigit(years[startindex()]))
                {
                    i--;
                    break;
                }
                i++;
            }

            return i > 0
                ? (int?)Convert.ToInt32(years[startindex()..])
                : null;
        }

        public static Person ToPerson(this PersonResponse src)
        {
            if (src is null)
                return null;

            var res = new Person()
            {
                Name = src.GetLocalName(),
                PremiereDate = src.Birthday.ParseDate(),
                EndDate = src.Death.ParseDate()
            };

            if (!string.IsNullOrWhiteSpace(src.Birthplace))
                res.ProductionLocations = new[] { src.Birthplace };

            return res;
        }

        public static string GetLocalName(this PersonResponse src)
        {
            var res = src?.NameRu;
            if (string.IsNullOrWhiteSpace(res))
                res = src?.NameEn;
            return res;
        }

    }
}
