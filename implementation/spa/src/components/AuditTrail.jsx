import { useEffect, useMemo, useState } from 'react';
import apiCall from '../api/client';

const EVENT_BADGE_STYLES = {
  REQUEST_SUBMITTED: 'bg-blue-100 text-blue-800',
  REQUEST_APPROVED: 'bg-emerald-100 text-emerald-800',
  REQUEST_DENIED: 'bg-red-100 text-red-800',
  REQUEST_REJECTED: 'bg-red-100 text-red-800',
  REQUEST_REDEEMED: 'bg-purple-100 text-purple-800',
  DOWNLOAD_URL_ISSUED: 'bg-indigo-100 text-indigo-800',
  OTP_SENT: 'bg-amber-100 text-amber-800',
};

const RESULT_STYLES = {
  Success: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  Failure: 'bg-red-50 text-red-700 ring-red-200',
  PartialSuccess: 'bg-amber-50 text-amber-700 ring-amber-200',
};

function formatEventType(eventType) {
  if (!eventType) return 'Unknown';
  return eventType
    .toLowerCase()
    .split('_')
    .map((part) => part[0].toUpperCase() + part.slice(1))
    .join(' ');
}

function formatActor(actorId) {
  if (!actorId) return 'System';
  if (actorId.includes('@')) return actorId;
  return `User ${actorId}`;
}

function formatMetadataValue(value) {
  if (value === null || value === undefined) return '—';
  if (typeof value === 'object') return JSON.stringify(value, null, 2);
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  return String(value);
}

function getRelativeTimeLabel(value) {
  const timestamp = new Date(value);
  if (Number.isNaN(timestamp.getTime())) return 'Unknown time';

  const seconds = Math.floor((Date.now() - timestamp.getTime()) / 1000);
  if (seconds < 0) return 'In the future';
  if (seconds < 60) return 'Just now';

  const ranges = [
    ['year', 60 * 60 * 24 * 365],
    ['month', 60 * 60 * 24 * 30],
    ['week', 60 * 60 * 24 * 7],
    ['day', 60 * 60 * 24],
    ['hour', 60 * 60],
    ['minute', 60],
  ];

  for (const [unit, amount] of ranges) {
    const count = Math.floor(seconds / amount);
    if (count >= 1) {
      return `${count} ${unit}${count > 1 ? 's' : ''} ago`;
    }
  }

  return 'Just now';
}

function EventMetadata({ metadata }) {
  const entries = metadata && typeof metadata === 'object' ? Object.entries(metadata) : [];

  if (entries.length === 0) {
    return null;
  }

  return (
    <details className="mt-3 rounded-lg border border-slate-200 bg-slate-50 p-3">
      <summary className="cursor-pointer text-sm font-semibold text-slate-700">
        Event metadata ({entries.length})
      </summary>
      <dl className="mt-3 space-y-2 text-sm">
        {entries.map(([key, value]) => (
          <div key={key} className="grid grid-cols-[9rem_1fr] gap-2">
            <dt className="font-medium text-slate-700">{key}</dt>
            <dd className="break-words text-slate-600">
              {typeof value === 'object' ? (
                <pre className="whitespace-pre-wrap rounded border border-slate-200 bg-white p-2 text-xs text-slate-700">
                  {formatMetadataValue(value)}
                </pre>
              ) : (
                formatMetadataValue(value)
              )}
            </dd>
          </div>
        ))}
      </dl>
    </details>
  );
}

function TimelineEvent({ event, isLast }) {
  const eventType = event.eventType ?? event.type;
  const badgeClass = EVENT_BADGE_STYLES[eventType] ?? 'bg-slate-100 text-slate-700';
  const resultClass = RESULT_STYLES[event.result] ?? 'bg-slate-50 text-slate-700 ring-slate-200';
  const absoluteTime = event.createdAt ? new Date(event.createdAt).toLocaleString() : 'Unknown time';
  const relativeTime = event.createdAt ? getRelativeTimeLabel(event.createdAt) : 'Unknown time';

  return (
    <li className="relative pl-10">
      <span className="absolute left-0 top-2.5 flex h-5 w-5 items-center justify-center rounded-full border-2 border-slate-100 bg-slate-900 shadow-sm" aria-hidden="true">
        <span className="h-1.5 w-1.5 rounded-full bg-white" />
      </span>
      {!isLast && <span className="absolute left-[9px] top-8 h-[calc(100%-0.9rem)] w-0.5 bg-gradient-to-b from-slate-300 to-slate-100" aria-hidden="true" />}

      <article className="relative overflow-hidden rounded-xl border border-slate-200 bg-white p-4 shadow-sm transition hover:shadow-md sm:p-5">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-slate-700 via-blue-600 to-indigo-600"
        />

        <div className="flex flex-wrap items-center gap-2 pt-1">
          <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${badgeClass}`}>
            {formatEventType(eventType)}
          </span>
          <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-medium ring-1 ring-inset ${resultClass}`}>
            {event.result ?? 'Unknown result'}
          </span>
        </div>

        <p className="mt-3 text-sm leading-relaxed text-slate-800">{event.description ?? 'No description provided.'}</p>

        <dl className="mt-4 grid gap-2 rounded-lg bg-slate-50/80 p-3 text-sm sm:grid-cols-2">
          <div>
            <dt className="font-medium text-slate-700">Actor</dt>
            <dd className="text-slate-600">{formatActor(event.actorId ?? event.actor)}</dd>
          </div>
          <div>
            <dt className="font-medium text-slate-700">Timestamp</dt>
            <dd className="text-slate-600">
              <time dateTime={event.createdAt ?? ''} title={absoluteTime}>
                {relativeTime} ({absoluteTime})
              </time>
            </dd>
          </div>
        </dl>

        <EventMetadata metadata={event.metadata} />

        {event.errorMessage && (
          <p className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
            Error: {event.errorMessage}
          </p>
        )}
      </article>
    </li>
  );
}

export default function AuditTrail({ requestId }) {
  const [events, setEvents] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    let isMounted = true;

    async function loadAuditTrail() {
      if (!requestId) {
        if (isMounted) {
          setIsLoading(false);
          setEvents([]);
          setError('A request ID is required to view the audit trail.');
        }
        return;
      }

      setIsLoading(true);
      setError('');

      try {
        const response = await apiCall(`/requests/${requestId}/audit`);
        const nextEvents = Array.isArray(response) ? response : response?.events;
        if (isMounted) {
          setEvents(Array.isArray(nextEvents) ? nextEvents : []);
        }
      } catch (loadError) {
        if (isMounted) {
          setEvents([]);
          setError(loadError.message ?? 'Unable to load audit trail.');
        }
      } finally {
        if (isMounted) {
          setIsLoading(false);
        }
      }
    }

    void loadAuditTrail();
    return () => {
      isMounted = false;
    };
  }, [requestId]);

  const timelineEvents = useMemo(
    () =>
      [...events].sort((left, right) => {
        const leftTime = new Date(left.createdAt ?? 0).getTime();
        const rightTime = new Date(right.createdAt ?? 0).getTime();
        return rightTime - leftTime;
      }),
    [events],
  );

  const summary = useMemo(() => {
    const total = timelineEvents.length;
    const failures = timelineEvents.filter((event) => event.result === 'Failure').length;
    const partial = timelineEvents.filter((event) => event.result === 'PartialSuccess').length;
    return { total, failures, partial };
  }, [timelineEvents]);

  return (
    <section
      className="rounded-2xl border border-slate-300 bg-gradient-to-b from-white to-slate-50 p-4 shadow-sm sm:p-5"
      aria-labelledby="audit-trail-title"
    >
      <div className="mb-4 rounded-xl border border-slate-200 bg-slate-900 px-4 py-3 text-white">
        <h3 id="audit-trail-title" className="text-base font-semibold tracking-wide sm:text-lg">
          Audit Trail
        </h3>
        <p className="mt-1 text-xs text-slate-300 sm:text-sm">Chronological security and access events for this request.</p>
      </div>

      {!isLoading && !error && timelineEvents.length > 0 && (
        <div className="mb-4 grid gap-2 sm:grid-cols-3">
          <div className="rounded-lg border border-slate-200 bg-white px-3 py-2">
            <p className="text-xs uppercase tracking-wide text-slate-500">Total events</p>
            <p className="text-lg font-semibold text-slate-900">{summary.total}</p>
          </div>
          <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2">
            <p className="text-xs uppercase tracking-wide text-red-700">Failures</p>
            <p className="text-lg font-semibold text-red-800">{summary.failures}</p>
          </div>
          <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2">
            <p className="text-xs uppercase tracking-wide text-amber-700">Partial success</p>
            <p className="text-lg font-semibold text-amber-800">{summary.partial}</p>
          </div>
        </div>
      )}

      {isLoading && (
        <div className="rounded-lg border border-slate-200 bg-white px-4 py-6 text-sm text-slate-600" aria-live="polite">
          Loading forensic timeline…
        </div>
      )}

      {!isLoading && error && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
          {error}
        </div>
      )}

      {!isLoading && !error && timelineEvents.length === 0 && (
        <div className="rounded-lg border border-slate-200 bg-white px-4 py-6 text-sm text-slate-600">
          No audit events found for this request.
        </div>
      )}

      {!isLoading && !error && timelineEvents.length > 0 && (
        <ol className="space-y-4" role="list">
          {timelineEvents.map((event, index) => (
            <TimelineEvent key={event.id ?? `${event.eventType}-${event.createdAt}-${index}`} event={event} isLast={index === timelineEvents.length - 1} />
          ))}
        </ol>
      )}
    </section>
  );
}
