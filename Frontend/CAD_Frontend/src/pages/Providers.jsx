import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';

export default function Providers({
  application,
  providers,
  selectedProvider,
  onSelect,
  onEdit,
  onAdd,
  onDelete
}) {
  const navigate = useNavigate();

  // ✅ AUTO REDIRECT if no application selected
  useEffect(() => {
    if (!application) {
      navigate('/applications');
    }
  }, [application]);

  if (!application) {
    return null; // prevent flicker
  }

  const getInitials = (code) => code?.slice(0, 2).toUpperCase() || '??';

  const avatarColors = [
    { bg: '#eff6ff', color: '#2563eb' },
    { bg: '#f0fdf4', color: '#16a34a' },
    { bg: '#fff7ed', color: '#ea580c' },
    { bg: '#fdf4ff', color: '#9333ea' },
    { bg: '#fff1f2', color: '#e11d48' },
  ];

  return (
    <section className="page">
      <header className="page-header">
        <div>
          <h1>Providers</h1>
          <p>Select a provider to manage its folder paths, procedures, field mappings and settings.</p>
        </div>

        <button
          type="button"
          className="primary large-button"
          onClick={onAdd}
        >
          + Add provider
        </button>
      </header>

      {providers.length === 0 ? (
        <div className="panel">
          <p className="empty-state">
            No providers found for this application. Click Add provider to get started.
          </p>
        </div>
      ) : (
        <div className="provider-card-grid">
          {providers.map((prov, i) => {
            const color = avatarColors[i % avatarColors.length];
            const isSelected =
              selectedProvider?.providerId === prov.providerId;

            return (
              <div
                key={prov.providerId}
                className="provider-card"
                style={
                  isSelected
                    ? {
                        borderColor: '#2563eb',
                        boxShadow: '0 0 0 3px rgba(37,99,235,.1)',
                      }
                    : {}
                }
              >
                <div
                  className="provider-card__avatar"
                  style={{ background: color.bg, color: color.color }}
                >
                  {getInitials(prov.providerCode)}
                </div>

                <div>
                  <div className="provider-card__name">
                    {prov.providerCode}
                  </div>

                  <div className="provider-card__meta">
                    {prov.identificationPath && (
                      <div>{prov.identificationPath}</div>
                    )}
                    {prov.identifierValue && (
                      <div>{prov.identifierValue}</div>
                    )}
                    {!prov.identificationPath &&
                      !prov.identifierValue && (
                        <div>No path configured</div>
                      )}
                  </div>
                </div>

                <div className="provider-card__actions">
                  <button
                    type="button"
                    className="primary"
                    style={{
                      flex: 1,
                      justifyContent: 'center',
                      fontSize: '13px',
                      padding: '7px',
                    }}
                    onClick={() => {
                      onSelect(prov);         // ✅ sets provider
                      navigate('/provider-detail'); // 🔥 NAVIGATION FIX
                    }}
                  >
                    View settings
                  </button>

                  <button
                    type="button"
                    className="secondary"
                    style={{ fontSize: '13px', padding: '7px 12px' }}
                    onClick={() => onEdit(prov)}
                  >
                    Edit
                  </button>

                  <button
                    type="button"
                    className="danger"
                    style={{ fontSize: '13px', padding: '7px 12px' }}
                    onClick={() => onDelete(prov.providerId)}
                  >
                    Delete
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </section>
  );
}