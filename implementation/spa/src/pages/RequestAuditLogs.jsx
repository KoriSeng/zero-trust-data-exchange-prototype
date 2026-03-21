import { useEffect, useMemo, useState } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import apiCall from '../api/client';
import { useAuth } from '../contexts/AuthContext';

function formatEventType(eventType) {
  if (!eventType) return 'Unknown';
  return eventType
    .toLowerCase()
    .split('_')
    .map((part) => part[0].toUpperCase() + part.slice(1))
    .join(' ');
}

function formatTime(value) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 'Unknown';
  return date.toLocaleString();
}

export default function RequestAuditLogs() {
  const { id: requestId } = useParams();
  const location = useLocation();
  const { profile } = useAuth();
  const [events, setEvents] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [eventTypeFilter, setEventTypeFilter] = useState('all');
  const [resultFilter, setResultFilter] = useState('all');
  const [query, setQuery] = useState('');
  const [sortDirection, setSortDirection] = useState('desc');

  const roles = profile?.roles ?? [];
  const canViewAudit =
    roles.length === 0 ||
    roles.includes('Requester') ||
    roles.includes('DataOwner') ||
    roles.includes('Admin');

  useEffect(() => {
    let active = true;

    async function loadAuditLogs() {
      if (!requestId) {
        setError('Missing request ID.');
        setIsLoading(false);
        return;
      }

      setIsLoading(true);
      setError('');

      try {
        const response = await apiCall(`/requests/${requestId}/audit`);
        if (active) {
          setEvents(Array.isArray(response) ? response : []);
        }
      } catch (loadError) {
        if (active) {
          setEvents([]);
          setError(loadError.message ?? 'Could not load audit logs.');
        }
      } finally {
        if (active) {
          setIsLoading(false);
        }
      }
    }

    void loadAuditLogs();
    return () => {
      active = false;
    };
  }, [requestId]);

  const eventTypeOptions = useMemo(() => {
    const uniqueTypes = new Set(events.map((event) => event.eventType).filter(Boolean));
    return ['all', ...Array.from(uniqueTypes)];
  }, [events]);

  const filteredEvents = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase();

    return [...events]
      .filter((event) => (eventTypeFilter === 'all' ? true : event.eventType === eventTypeFilter))
      .filter((event) => (resultFilter === 'all' ? true : event.result === resultFilter))
      .filter((event) => {
        if (!normalizedQuery) return true;
        const haystack = [
          event.eventType,
          event.result,
          event.actorId,
          event.description,
          event.errorMessage,
          JSON.stringify(event.metadata ?? {}),
        ]
          .filter(Boolean)
          .join(' ')
          .toLowerCase();
        return haystack.includes(normalizedQuery);
      })
      .sort((a, b) => {
        const left = new Date(a.createdAt ?? 0).getTime();
        const right = new Date(b.createdAt ?? 0).getTime();
        return sortDirection === 'desc' ? right - left : left - right;
      });
  }, [events, eventTypeFilter, resultFilter, query, sortDirection]);

  const backTo = location.state?.from ?? '/requests';

  if (!canViewAudit) {
    return (
      <section className="content-page">
        <p className="error-banner">Access denied: requester, data owner, or admin role is required.</p>
      </section>
    );
  }

  return (
    <section className="content-page stack-gap">
      <div className="section-header">
        <div>
          <h2>Audit Logs</h2>
          <p className="muted-text">Request ID: {requestId}</p>
        </div>
        <Link className="secondary-button" to={backTo}>
          Back
        </Link>
      </div>

      <div className="card details-grid">
        <div>
          <label htmlFor="audit-event-type">Event type</label>
          <select
            id="audit-event-type"
            value={eventTypeFilter}
            onChange={(event) => setEventTypeFilter(event.target.value)}
          >
            {eventTypeOptions.map((type) => (
              <option key={type} value={type}>
                {type === 'all' ? 'All event types' : formatEventType(type)}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label htmlFor="audit-result">Result</label>
          <select id="audit-result" value={resultFilter} onChange={(event) => setResultFilter(event.target.value)}>
            <option value="all">All results</option>
            <option value="Success">Success</option>
            <option value="Failure">Failure</option>
            <option value="PartialSuccess">Partial success</option>
          </select>
        </div>
        <div>
          <label htmlFor="audit-sort">Sort by time</label>
          <select
            id="audit-sort"
            value={sortDirection}
            onChange={(event) => setSortDirection(event.target.value)}
          >
            <option value="desc">Newest first</option>
            <option value="asc">Oldest first</option>
          </select>
        </div>
        <div>
          <label htmlFor="audit-query">Search</label>
          <input
            id="audit-query"
            type="text"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Search actor, description, metadata..."
          />
        </div>
      </div>

      {error && <p className="error-banner">{error}</p>}

      {isLoading ? (
        <p>Loading audit logs…</p>
      ) : filteredEvents.length === 0 ? (
        <p>No audit events match the current filters.</p>
      ) : (
        <div className="table-wrapper">
          <table>
            <thead>
              <tr>
                <th>Time</th>
                <th>Event</th>
                <th>Result</th>
                <th>Actor</th>
                <th>Description</th>
                <th>Metadata</th>
              </tr>
            </thead>
            <tbody>
              {filteredEvents.map((event) => (
                <tr key={event.id ?? `${event.eventType}-${event.createdAt}`}>
                  <td>{formatTime(event.createdAt)}</td>
                  <td>{formatEventType(event.eventType)}</td>
                  <td>{event.result ?? 'Unknown'}</td>
                  <td>{event.actorId ?? 'System'}</td>
                  <td>{event.description ?? '—'}</td>
                  <td>
                    {event.metadata && Object.keys(event.metadata).length > 0 ? (
                      <details>
                        <summary>View</summary>
                        <pre>{JSON.stringify(event.metadata, null, 2)}</pre>
                      </details>
                    ) : (
                      '—'
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
