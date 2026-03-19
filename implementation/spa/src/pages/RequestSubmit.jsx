import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import apiCall from '../api/client';

export default function RequestSubmit() {
  const navigate = useNavigate();

  const [form, setForm] = useState({
    datasetId: '',
    purpose: '',
  });
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');

  function updateField(event) {
    const { name, value } = event.target;
    setForm((current) => ({
      ...current,
      [name]: value,
    }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setError('');
    setIsSubmitting(true);

    const payload = {
      datasetId: form.datasetId.trim(),
      purpose: form.purpose.trim(),
    };

    try {
      const response = await apiCall('/requests', {
        method: 'POST',
        body: JSON.stringify(payload),
      });

      navigate('/requests', {
        replace: true,
        state: { newRequestId: response.requestId },
      });
    } catch (submitError) {
      setError(submitError.message ?? 'Request submission failed.');
      setIsSubmitting(false);
    }
  }

  return (
    <section className="content-page stack-gap">
      <h2>Submit Data Access Request</h2>

      <form className="stack-form card" onSubmit={handleSubmit}>
        <label>
          Dataset ID
          <input
            name="datasetId"
            value={form.datasetId}
            onChange={updateField}
            required
            placeholder="dataset-imaging-ct-2025"
          />
          <small className="muted-text">
            Enter the ID of a published dataset. Dataset details will be fetched automatically.
          </small>
        </label>

        <label>
          Purpose
          <textarea
            name="purpose"
            rows={4}
            value={form.purpose}
            onChange={updateField}
            required
            placeholder="Why this access is needed (e.g., 'For cancer research project ABC-123')"
          />
        </label>

        {error && <p className="error-inline">{error}</p>}

        <button type="submit" className="primary-button" disabled={isSubmitting}>
          {isSubmitting ? 'Submitting…' : 'Submit request'}
        </button>
      </form>
    </section>
  );
}
