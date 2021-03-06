﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ProductionProfiler.Core.Persistence;
using ProductionProfiler.Core.Persistence.Entities;
using ProductionProfiler.Core.Profiling.Entities;
using ProductionProfiler.Core.RequestTiming.Entities;
using System.Linq;
using Raven.Client;
using Raven.Client.Linq;
using ProductionProfiler.Core.Extensions;

namespace ProductionProfiler.Persistence.Raven
{
    public class RavenProfilerRepository : IProfilerRepository
    {
		private readonly IDocumentStore _database;
		private readonly RavenConfiguration _ravenConfiguration;

        public RavenProfilerRepository(IDocumentStore database, RavenConfiguration ravenConfiguration)
        {
	        _database = database;
	        _ravenConfiguration = ravenConfiguration;
        }

	    public Core.Persistence.Entities.Page<UrlToProfile> GetUrlsToProfile(PagingInfo pagingInfo)
        {
            using (var session = _ravenConfiguration.DatabaseName.IsNullOrEmpty() ? _database.OpenSession() : _database.OpenSession(_ravenConfiguration.DatabaseName))
            {
                RavenQueryStatistics stats;
                var documentQuery = session.Query<UrlToProfile>()
                    .Customize(c => c.WaitForNonStaleResults(new TimeSpan(0, 0, 3)))
                    .Statistics(out stats)
                    .Skip((pagingInfo.PageNumber - 1)*pagingInfo.PageSize)
                    .Take(pagingInfo.PageSize);

                return new Core.Persistence.Entities.Page<UrlToProfile>(
                    documentQuery.ToList(), 
                    new Pagination(pagingInfo.PageSize, pagingInfo.PageNumber, stats.TotalResults));
            }
        }

        public UrlToProfile GetUrlToProfile(string url)
        {
			using (var session = _ravenConfiguration.DatabaseName.IsNullOrEmpty() ? _database.OpenSession() : _database.OpenSession(_ravenConfiguration.DatabaseName))
            {
                return Queryable.Where(session.Query<UrlToProfile>(), r => r.Url == url).FirstOrDefault();
            }
        }

        public List<UrlToProfile> GetCurrentUrlsToProfile()
        {
			using (var session = _ravenConfiguration.DatabaseName.IsNullOrEmpty() ? _database.OpenSession() : _database.OpenSession(_ravenConfiguration.DatabaseName))
            {
                return session.Query<UrlToProfile>()
                    .Where(req => req.Enabled && (req.ProfilingCount == null || req.ProfilingCount > 0))
                    .ToList();
            }
        }

        public void SaveUrlToProfile(UrlToProfile urlToProfile)
        {
			using (var session = _ravenConfiguration.DatabaseName.IsNullOrEmpty() ? _database.OpenSession() : _database.OpenSession(_ravenConfiguration.DatabaseName))
            {
                // HRB: removed this code as it's causing problems ("Error: Attempted to associated a different object with id"),
                // and it's not necessary - Raven will simply overwrite the stored profile, which is what we want.
                
                /*
                var request = session.Query<UrlToProfile>(UrlToProfileIndexName)
                    .Where(r => r.Url == UrlToProfile.Url)
                    .FirstOrDefault();

                //if one exists delete it and replace with the new incoming request
                if (request != null)
                {
                    session.Delete(request);
                }
                */

                session.Store(urlToProfile);
                session.SaveChanges();
            }
        }

        public void DeleteUrlToProfile(string url)
        {
			using (var session = _ravenConfiguration.DatabaseName.IsNullOrEmpty() ? _database.OpenSession() : _database.OpenSession(_ravenConfiguration.DatabaseName))
            {
                var item = Queryable.Where(session.Query<UrlToProfile>(), r => r.Url == url).FirstOrDefault();

                if (item != null)
                {
                    session.Delete(item);
                    session.SaveChanges();
                }
            }
        }

        public Core.Persistence.Entities.Page<string> GetDistinctUrlsToProfile(PagingInfo pagingInfo)
        {
			using (var session = _ravenConfiguration.DatabaseName.IsNullOrEmpty() ? _database.OpenSession() : _database.OpenSession(_ravenConfiguration.DatabaseName))
            {
                //TODO: need map-reduce for this (to get distinct)
                RavenQueryStatistics stats;
                var items = session.Query<ProfiledRequestCount, ProfiledRequestDataCountsByUrl>()
                    .Statistics(out stats)
                    .OrderByDescending(url => url.MostRecentUtc)
                    .Skip((pagingInfo.PageNumber - 1)*pagingInfo.PageSize)
                    .Take(pagingInfo.PageSize)
                    .ToList();

                return new Core.Persistence.Entities.Page<string>(
                    items.Select(i => i.Url),
                    new Pagination(pagingInfo.PageSize, pagingInfo.PageNumber, items.Count));
            }
        }

        public ProfiledRequestData GetProfiledRequestDataById(Guid id)
        {
			using (var session = _ravenConfiguration.DatabaseName.IsNullOrEmpty() ? _database.OpenSession() : _database.OpenSession(_ravenConfiguration.DatabaseName))
            {
                return session.Load<ProfiledRequestData>(id);
            }
        }

        public void DeleteProfiledRequestDataById(Guid id)
        {
			using (var session = _ravenConfiguration.DatabaseName.IsNullOrEmpty() ? _database.OpenSession() : _database.OpenSession(_ravenConfiguration.DatabaseName))
            {
                var item = session.Load<ProfiledRequestData>(id);

                if (item != null)
                {
                    session.Delete(item);
                    session.SaveChanges();
                }
            }
        }

        public void SaveProfiledRequestData(ProfiledRequestData data)
        {
			using (var session = _ravenConfiguration.DatabaseName.IsNullOrEmpty() ? _database.OpenSession() : _database.OpenSession(_ravenConfiguration.DatabaseName))
            {
                session.Store(data);
                session.SaveChanges();
            }
        }

        public Core.Persistence.Entities.Page<ProfiledRequestDataPreview> GetProfiledRequestDataPreviewByUrl(string url, PagingInfo pagingInfo)
        {
            return DoGetPreview(r => r.Url == url, pagingInfo);
        }

        public Core.Persistence.Entities.Page<ProfiledRequestDataPreview> GetProfiledRequestDataPreviewBySessionId(Guid sessionId, PagingInfo pagingInfo)
        {
            return DoGetPreview(r => r.SessionId == sessionId, pagingInfo);
        }

        public Core.Persistence.Entities.Page<ProfiledRequestDataPreview> GetProfiledRequestDataPreviewBySessionUserId(string sessionUserId, PagingInfo pagingInfo)
        {
            return DoGetPreview(r => r.SessionUserId == sessionUserId, pagingInfo);
        }

        public Core.Persistence.Entities.Page<ProfiledRequestDataPreview> GetProfiledRequestDataPreviewBySamplingId(Guid samplingId, PagingInfo pagingInfo)
        {
            return DoGetPreview(r => r.SamplingId == samplingId, pagingInfo);
        }

        private Core.Persistence.Entities.Page<ProfiledRequestDataPreview> DoGetPreview(Expression<Func<ProfiledRequestData, bool>> whereClause, PagingInfo pagingInfo)
        {
			using (var session = _ravenConfiguration.DatabaseName.IsNullOrEmpty() ? _database.OpenSession() : _database.OpenSession(_ravenConfiguration.DatabaseName))
            {
                RavenQueryStatistics stats;
                var q = session.Query<ProfiledRequestData>()
                    .Where(whereClause)
                    .OrderByDescending(r => r.CapturedOnUtc)
                    .Statistics(out stats)
                    .Skip((pagingInfo.PageNumber - 1)*pagingInfo.PageSize)
                    .Take(pagingInfo.PageSize)
                    .Select(r => new ProfiledRequestDataPreview
                                     {
                                         CapturedOnUtc = r.CapturedOnUtc,
                                         ElapsedMilliseconds = r.ElapsedMilliseconds,
                                         Id = r.Id,
                                         Server = r.Server,
                                         Url = r.Url
                                     });

                var pagination = new Pagination(pagingInfo.PageSize, pagingInfo.PageNumber, stats.TotalResults);

                return new Core.Persistence.Entities.Page<ProfiledRequestDataPreview>(q.ToList(), pagination);
            }
        }

        public void DeleteProfiledRequestDataByUrl(string url)
        {
			using (var session = _ravenConfiguration.DatabaseName.IsNullOrEmpty() ? _database.OpenSession() : _database.OpenSession(_ravenConfiguration.DatabaseName))
            {
                foreach (var document in session.Query<ProfiledRequestData>().Where(r => r.Url == url))
                {
                    session.Delete(document);
                }
                session.SaveChanges();
            }
        }

        public void SaveResponse(ProfiledResponse response)
        {
			using (var session = _ravenConfiguration.DatabaseName.IsNullOrEmpty() ? _database.OpenSession() : _database.OpenSession(_ravenConfiguration.DatabaseName))
            {
                session.Store(response);
                session.SaveChanges();
            }
        }

        public void SaveTimedRequest(TimedRequest timedRequest)
        {
			using (var session = _ravenConfiguration.DatabaseName.IsNullOrEmpty() ? _database.OpenSession() : _database.OpenSession(_ravenConfiguration.DatabaseName))
            {
                session.Store(timedRequest);
                session.SaveChanges();
            }
        }

        public Core.Persistence.Entities.Page<TimedRequest> GetLongRequests(PagingInfo paging)
        {
			using (var session = _ravenConfiguration.DatabaseName.IsNullOrEmpty() ? _database.OpenSession() : _database.OpenSession(_ravenConfiguration.DatabaseName))
			{
				RavenQueryStatistics stats;
				var items = session.Query<TimedRequest>()
					.Statistics(out stats)
					.Skip((paging.PageNumber - 1) * paging.PageSize)
					.Take(paging.PageSize)
					.ToList();

				return new Core.Persistence.Entities.Page<TimedRequest>(items, new Pagination(paging.PageSize, paging.PageNumber, items.Count));
			}
        }

        public void DeleteAllTimedRequests()
        {
			using (var session = _ravenConfiguration.DatabaseName.IsNullOrEmpty() ? _database.OpenSession() : _database.OpenSession(_ravenConfiguration.DatabaseName))
			{
				foreach (var timedRequest in session.Query<TimedRequest>())
				{
					session.Delete(timedRequest);
				}
			}
        }

        public ProfiledResponse GetResponseById(Guid id)
        {
			using (var session = _ravenConfiguration.DatabaseName.IsNullOrEmpty() ? _database.OpenSession() : _database.OpenSession(_ravenConfiguration.DatabaseName))
            {
                return session.Load<ProfiledResponse>(id);
            }
        }

        public void DeleteResponseById(Guid id)
        {
			using (var session = _ravenConfiguration.DatabaseName.IsNullOrEmpty() ? _database.OpenSession() : _database.OpenSession(_ravenConfiguration.DatabaseName))
            {
                var item = session.Load<ProfiledResponse>(id);

                if (item != null)
                {
                    session.Delete(item);
                    session.SaveChanges();
                }
            }
        }

        public void DeleteResponseByUrl(string url)
        {
			using (var session = _ravenConfiguration.DatabaseName.IsNullOrEmpty() ? _database.OpenSession() : _database.OpenSession(_ravenConfiguration.DatabaseName))
            {
                foreach (var document in session.Query<ProfiledResponse>().Where(r => r.Url == url))
                {
                    session.Delete(document);
                }
                session.SaveChanges();
            }
        }
    }
}
