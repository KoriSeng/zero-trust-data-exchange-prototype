import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import apiCall from '../api/client';
import { useAuth } from '../contexts/AuthContext';

export default function DatasetExplorer() {
  const navigate = useNavigate();
  const { profile } = useAuth();

  const roles = profile?.roles ?? [];
  const canSubmitRequests =
    roles.length === 0 || roles.includes('Requester') || roles.includes('Admin');

  const [datasets, setDatasets] = useState([]);
  const [selectedIds, setSelectedIds] = useState([]);
  const [purpose, setPurpose] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');

  const selectedDatasets = useMemo(
    () => datasets.filter((dataset) => selectedIds.includes(dataset.id)),
    [datasets, selectedIds],
  );

  const loadDatasets = useCallback(async () => {
    try {
      const response = await apiCall('/datasets');
      setDatasets(Array.isArray(response) ? response : []);
      setError('');
    } catch (loadError) {
      setError(loadError.message ?? 'Could not load datasets.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadDatasets();
  }, [loadDatasets]);

  function toggleDatasetSelection(datasetId) {
    setSelectedIds((current) =>
      current.includes(datasetId)
        ? current.filter((id) => id !== datasetId)
        : [...current, datasetId],
    );
  }

  async function createRequestsFromCart() {
    if (!purpose.trim()) {
      setError('Please provide a purpose for access.');
      return;
    }

    if (selectedDatasets.length === 0) {
      setError('Please select at least one dataset.');
      return;
    }

    setError('');
    setIsSubmitting(true);

    const createdRequestIds = [];
    try {
      for (const dataset of selectedDatasets) {
        const response = await apiCall('/requests', {
          method: 'POST',
          body: JSON.stringify({
            datasetId: dataset.datasetId,
            purpose: purpose.trim(),
          }),
        });

        if (response?.requestId) {
          createdRequestIds.push(response.requestId);
        }
      }

      navigate('/requests', {
        replace: true,
        state: { newRequestIds: createdRequestIds },
      });
    } catch (submitError) {
      setError(submitError.message ?? 'Could not submit request(s).');
      setIsSubmitting(false);
    }
  }

  if (!canSubmitRequests) {
    return (
      <section className="content-page">
        <p className="error-banner">Access denied: requester role is required.</p>
      </section>
    );
  }

  return (
    <section className="content-page stack-gap">
      <div className="section-header">
        <h2>Dataset Explorer</h2>
      </div>

      <p className="muted-text">
        Browse published datasets, add them to cart, then submit one request per selected dataset.
      </p>

      {error && <p className="error-banner">{error}</p>}

      {isLoading ? (
        <p>Loading datasets…</p>
      ) : datasets.length === 0 ? (
        <p>No datasets available.</p>
      ) : (
        <div className="dataset-grid">
          {datasets.map((dataset) => {
            const isSelected = selectedIds.includes(dataset.id);
            return (
              <article className={`card dataset-card${isSelected ? ' dataset-card-selected' : ''}`} key={dataset.id}>
                <div className="dataset-thumbnail-wrap">
                  {dataset.thumbnailUrl ? (
                    <img src={dataset.thumbnailUrl} alt={`${dataset.name} preview`} className="dataset-thumbnail" />
                  ) : (
                    <div className="dataset-thumbnail dataset-thumbnail-fallback">No preview</div>
                  )}
                </div>

                <h3>{dataset.name}</h3>
                <p className="muted-text">{dataset.summary}</p>

                <p className="dataset-meta">
                  Owner: <strong>{dataset.dataOwnerOrgName ?? dataset.dataOwnerOrg}</strong>
                </p>
                <p className="dataset-meta">
                  Records: <strong>{dataset.recordCount?.toLocaleString?.() ?? dataset.recordCount}</strong>
                </p>
                <p className="dataset-meta">
                  Updated: <strong>{new Date(dataset.lastUpdatedAt).toLocaleDateString()}</strong>
                </p>

                <div className="tag-row">
                  {(dataset.tags ?? []).map((tag) => (
                    <span key={tag} className="tag-pill">
                      {tag}
                    </span>
                  ))}
                </div>

                <button
                  type="button"
                  className={isSelected ? 'secondary-button' : 'primary-button'}
                  onClick={() => toggleDatasetSelection(dataset.id)}
                >
                  {isSelected ? 'Remove from cart' : 'Add to cart'}
                </button>
              </article>
            );
          })}
        </div>
      )}

      <section className="card stack-gap">
        <h3>Request cart ({selectedDatasets.length})</h3>
        <label>
          Purpose for access
          <textarea
            rows={3}
            value={purpose}
            onChange={(event) => setPurpose(event.target.value)}
            placeholder="Describe your research or analysis purpose"
            required
          />
        </label>
        <button
          type="button"
          className="primary-button"
          onClick={createRequestsFromCart}
          disabled={isSubmitting || selectedDatasets.length === 0}
        >
          {isSubmitting ? 'Submitting…' : `Submit ${selectedDatasets.length} request(s)`}
        </button>
      </section>
    </section>
  );
}
