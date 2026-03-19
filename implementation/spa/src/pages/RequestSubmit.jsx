import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import apiCall from '../api/client';

export default function RequestSubmit() {
  const navigate = useNavigate();

  const [form, setForm] = useState({
    datasetId: '',
    datasetName: '',
    purpose: '',
    objectKeys: '',
    dataOwnerOrg: 'ORG-B',
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
      datasetName: form.datasetName.trim(),
      purpose: form.purpose.trim(),
      objectKeys: form.objectKeys
        .split(/[\n,]/)
        .map((key) => key.trim())
        .filter(Boolean),
      dataOwnerOrg: form.dataOwnerOrg,
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
            placeholder="DS-001"
          />
        </label>

        <label>
          Dataset Name
          <input
            name="datasetName"
            value={form.datasetName}
            onChange={updateField}
            required
            placeholder="Sensitive Research Dataset"
          />
        </label>

        <label>
          Purpose
          <textarea
            name="purpose"
            rows={3}
            value={form.purpose}
            onChange={updateField}
            required
            placeholder="Why this access is needed"
          />
        </label>

        <label>
          Object Keys (comma or newline separated)
          <textarea
            name="objectKeys"
            rows={4}
            value={form.objectKeys}
            onChange={updateField}
            required
            placeholder="data/sample.csv, data/sample-2.csv"
          />
        </label>

        <label>
          Data owner organisation
          <select name="dataOwnerOrg" value={form.dataOwnerOrg} onChange={updateField}>
            <option value="ORG-A">ORG-A</option>
            <option value="ORG-B">ORG-B</option>
          </select>
        </label>

        {error && <p className="error-inline">{error}</p>}

        <button type="submit" className="primary-button" disabled={isSubmitting}>
          {isSubmitting ? 'Submitting…' : 'Submit request'}
        </button>
      </form>
    </section>
  );
}
