import { useEffect, useMemo, useState } from 'react';
import {
  getProviderFolders,
  saveProviderFolders,
  getProviderProcedures,
  saveProviderProcedure,
  deleteProviderProcedure,
  getFieldMappings,
  saveFieldMapping,
  deleteFieldMapping,
  getProviderRules,
  saveProviderRule,
  deleteProviderRule,
  getProviderServiceMetadataRows,
  saveProviderServiceMetadataRow,
  deleteProviderServiceMetadata,
  getProviderEmailSettings,
  saveProviderEmailSettings,
  getProviderRetrySettings,
  saveProviderRetrySettings,
} from '../services/api';
import EmailSettings from './EmailSettings';
import RetrySettings from './RetrySettings';

const sectionLabels = {
  folders: 'Folders',
  procedures: 'Procedures',
  mappings: 'Field mappings',
  rules: 'Rules',
  metadata: 'Service metadata',
  email: 'Email Settings',
  retry: 'Retry Settings',
};

export default function ProviderDetail({ application, provider }) {
  const [activeSection, setActiveSection] = useState('folders');
  const [folders, setFolders] = useState(null);
  const [procedures, setProcedures] = useState([]);
  const [mappings, setMappings] = useState([]);
  const [rules, setRules] = useState([]);
  const [metadataRows, setMetadataRows] = useState([]);
  const [loading, setLoading] = useState(false);

  const normalizeMetadataRow = (row) => ({
    cdServiceMetaDataId: row.cdServiceMetaDataId ?? row.CdServiceMetaDataId ?? row.CDServiceMetaDataId ?? 0,
    applicationId: row.applicationId ?? row.ApplicationId ?? 0,
    providerId: row.providerId ?? row.ProviderId ?? 0,
    operatorType: row.operatorType ?? row.OperatorType ?? 0,
    value: row.value ?? row.Value ?? '',
    orinNum: row.orinNum ?? row.ORINum ?? row.oRINum ?? row.oriNum ?? '',
    isActive: row.isActive ?? row.IsActive ?? false,
    createdBy: row.createdBy ?? row.CreatedBy ?? 0,
    createdDate: row.createdDate ?? row.CreatedDate ?? null,
    updatedBy: row.updatedBy ?? row.UpdatedBy ?? 0,
    updatedDate: row.updatedDate ?? row.UpdatedDate ?? null,
    tempId: row.tempId,
  });

  useEffect(() => {
    if (!provider) return;
    setLoading(true);
    const emptyFolderConfig = {
      folderConfigId: 0,
      applicationId: provider.applicationId ?? application?.applicationId ?? 0,
      providerId: provider.providerId,
      sourceFolder: '',
      doneFolder: '',
      errorFolder: '',
      retryFolder: '',
      otherAgencyFolder: '',
    };

    Promise.all([
      getProviderFolders(provider.providerId),
      getProviderProcedures(provider.providerId),
      getFieldMappings(provider.providerId),
      getProviderRules(provider.providerId),
      getProviderServiceMetadataRows(provider.providerId),
    ])
      .then(([foldersResult, proceduresResult, mappingsResult, rulesResult, metadataRowsResult]) => {
        setFolders(foldersResult ?? emptyFolderConfig);
        setProcedures(proceduresResult);
        setMappings(mappingsResult);
        setRules(rulesResult);
        setMetadataRows((metadataRowsResult || []).map(normalizeMetadataRow));
      })
      .catch(() => {
        setFolders(emptyFolderConfig);
        setProcedures([]);
        setMappings([]);
        setRules([]);
        setMetadataRows([]);
      })
      .finally(() => setLoading(false));
  }, [provider]);

  const canSaveFolderConfig = provider && folders;

  const handleFolderChange = (field, value) => {
    setFolders((prev) => ({ ...prev, [field]: value }));
  };

  const saveFolders = async () => {
    if (!provider || !folders) return;
    const payload = {
      ...folders,
      applicationId: folders.applicationId || application?.applicationId || provider.applicationId || 0,
    };

    try {
      await saveProviderFolders(provider.providerId, payload);
      setFolders(payload);
      window.alert('Folder Settings saved.');
    } catch (error) {
      console.error('Failed to save folder settings', error);
      window.alert('Unable to save folder settings. Check the console for details.');
    }
  };

  const addProcedure = () => {
    setProcedures((prev) => [
      ...prev,
      { procedureId: 0, procedureName: '', executionOrder: prev.length + 1, isRepeatable: false },
    ]);
  };

const addMapping = () => {
  if (procedures.length === 0 || !procedures[0]?.procedureId) {
    window.alert('Please add and save a Procedure first before adding Field Mappings.');
    return;
  }
  setMappings((prev) => [
    ...prev,
    { mappingId: 0, procedureId: procedures[0].procedureId, parameterName: '', xmlPath: '', isRequired: false, defaultValue: '' },
  ]);
};
  const addRule = () => {
  if (procedures.length === 0 || !procedures[0]?.procedureId) {
    window.alert('Please add and save a Procedure first before adding Rules.');
    return;
  }
  setRules((prev) => [
    ...prev,
    { ruleId: 0, procedureId: procedures[0].procedureId, parameterName: '', ruleType: '', ruleValue: '', ruleCategory: '', ruleOrder: prev.length + 1, isActive: true },
  ]);
};

  const updateItem = (list, setter, idField, idValue, field, value) => {
    setter(list.map((item) => (item[idField] === idValue ? { ...item, [field]: value } : item)));
  };

  const saveProcedure = async (procedure) => {
    try {
      const saved = await saveProviderProcedure(provider.providerId, procedure);
      setProcedures((prev) => prev.map((item) => (item === procedure ? (saved ?? procedure) : item)));
      window.alert('Procedure saved.');
    } catch (error) {
      console.error('Failed to save procedure', error);
      window.alert('Unable to save procedure. Check the console for details.');
    }
  };

  const saveMapping = async (mapping) => {
    try {
      const saved = await saveFieldMapping(provider.providerId, mapping);
      setMappings((prev) => prev.map((item) => (item === mapping ? (saved ?? mapping) : item)));
      window.alert('Mapping saved.');
    } catch (error) {
      console.error('Failed to save mapping', error);
      window.alert('Unable to save mapping. Check the console for details.');
    }
  };

  const saveRule = async (rule) => {
    try {
      const saved = await saveProviderRule(provider.providerId, rule);
      setRules((prev) => prev.map((item) => (item === rule ? (saved ?? rule) : item)));
      window.alert('Rule saved.');
    } catch (error) {
      console.error('Failed to save rule', error);
      window.alert('Unable to save rule. Check the console for details.');
    }
  };

  const saveMetadataRow = async (row) => {
    try {
      const payload = {
        ...row,
        ORINum: row.orinNum ?? row.ORINum ?? row.oRINum ?? row.oriNum ?? '',
        OperatorType: row.operatorType ?? row.OperatorType,
        Value: row.value ?? row.Value,
        IsActive: row.isActive ?? row.IsActive,
        CreatedDate: row.createdDate ?? row.CreatedDate,
        UpdatedDate: row.updatedDate ?? row.UpdatedDate,
        CdServiceMetaDataId: row.cdServiceMetaDataId ?? row.CdServiceMetaDataId ?? 0,
      };
      const saved = await saveProviderServiceMetadataRow(provider.providerId, payload);
      setMetadataRows((prev) => prev.map((item) => {
        const normalized = normalizeMetadataRow(saved);
        if (item.cdServiceMetaDataId && item.cdServiceMetaDataId === normalized.cdServiceMetaDataId) {
          return normalized;
        }
        if (item.tempId && item.tempId === row.tempId) {
          return normalized;
        }
        return item;
      }));
      window.alert('Metadata row saved.');
    } catch (error) {
      console.error('Failed to save metadata row', error);
      window.alert('Unable to save metadata row. Check the console for details.');
    }
  };

  const deleteMetadataRow = async (row) => {
    try {
      if (!row.cdServiceMetaDataId) {
        setMetadataRows((prev) => prev.filter((item) => item.tempId !== row.tempId));
        return;
      }
      await deleteProviderServiceMetadata(provider.providerId, row.cdServiceMetaDataId);
      setMetadataRows((prev) => prev.filter((item) => item.cdServiceMetaDataId !== row.cdServiceMetaDataId));
    } catch (error) {
      console.error('Failed to delete metadata row', error);
      window.alert('Unable to delete metadata row. Check the console for details.');
    }
  };

  const addMetadataRow = () => {
    setMetadataRows((prev) => [
      ...prev,
      {
        cdServiceMetaDataId: 0,
        tempId: `new-${Date.now()}`,
        operatorType: 1,
        value: '',
        orinNum: '',
        isActive: true,
      },
    ]);
  };

  const updateMetadataRow = (identifier, field, value) => {
    setMetadataRows((prev) => prev.map((item) => {
      if (item.cdServiceMetaDataId && item.cdServiceMetaDataId === identifier) {
        return { ...item, [field]: value };
      }
      if (item.tempId && item.tempId === identifier) {
        return { ...item, [field]: value };
      }
      return item;
    }));
  };

  const removeItem = async (list, setter, deleteFn, idField, idValue) => {
    if (!idValue) {
      setter(list.filter((item) => item[idField] !== idValue));
      return;
    }
    await deleteFn(idValue);
    setter(list.filter((item) => item[idField] !== idValue));
  };

  if (!provider) {
    return (
      <section className="page">
        <header className="page-header">
          <h1>Provider Settings</h1>
          <p>Select provider for managing the provider settings</p>
        </header>
      </section>
    );
  }

  if (loading) {
    return (
      <section className="page">
        <p>Loading provider Settings...</p>
      </section>
    );
  }
return (
    <section className="page">
      <header className="page-header">
        <div>
          <h1>{provider.providerCode} Settings</h1>
          <p>Folder paths · Procedures · Field mappings · Rules · Retry · Email</p>
        </div>
      </header>

      <div className="tabs">
        {Object.entries(sectionLabels).map(([key, label]) => (
          <button
            key={key}
            type="button"
            className={activeSection === key ? 'active' : ''}
            onClick={() => setActiveSection(key)}
          >
            {label}
          </button>
        ))}
      </div>

      {activeSection === 'folders' && folders && (
        <div className="panel">
          <div className="panel-header">
            <h2>Folder settings</h2>
          </div>
          {folders.folderConfigId === 0 && (
            <p style={{ padding: '10px 20px', fontSize: 13, color: '#6b7280' }}>
              No folder configuration yet. Fill in the paths below and click Save.
            </p>
          )}
          <table>
            <thead>
              <tr>
                <th>Source folder</th>
                <th>Done folder</th>
                <th>Error folder</th>
                <th>Retry folder</th>
                <th>Other agency</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><input value={folders.sourceFolder || ''} onChange={(e) => handleFolderChange('sourceFolder', e.target.value)} /></td>
                <td><input value={folders.doneFolder || ''} onChange={(e) => handleFolderChange('doneFolder', e.target.value)} /></td>
                <td><input value={folders.errorFolder || ''} onChange={(e) => handleFolderChange('errorFolder', e.target.value)} /></td>
                <td><input value={folders.retryFolder || ''} onChange={(e) => handleFolderChange('retryFolder', e.target.value)} /></td>
                <td><input value={folders.otherAgencyFolder || ''} onChange={(e) => handleFolderChange('otherAgencyFolder', e.target.value)} /></td>
                <td><button type="button" className="primary" onClick={saveFolders}>Save</button></td>
              </tr>
            </tbody>
          </table>
        </div>
      )}

      {activeSection === 'procedures' && (
        <div className="panel">
          <div className="panel-header">
            <h2>Procedures</h2>
            <button type="button" className="primary" onClick={addProcedure}>+ Add</button>
          </div>
          {procedures.length === 0 ? (
            <p className="empty-state">No procedures yet. Click + Add to create one.</p>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th style={{ width: 90 }}>Order</th>
                  <th style={{ width: 100 }}>Repeatable</th>
                  <th style={{ width: 130 }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {procedures.map((proc, i) => (
                  <tr key={`${proc.procedureId}-${i}`}>
                    <td>
                      <input
                        value={proc.procedureName || ''}
                        onChange={(e) => updateItem(procedures, setProcedures, 'procedureId', proc.procedureId, 'procedureName', e.target.value)}
                      />
                    </td>
                    <td>
                      <input
                        type="number"
                        value={proc.executionOrder || 0}
                        onChange={(e) => updateItem(procedures, setProcedures, 'procedureId', proc.procedureId, 'executionOrder', Number(e.target.value))}
                      />
                    </td>
                    <td style={{ textAlign: 'center' }}>
                      <input
                        type="checkbox"
                        checked={proc.isRepeatable || false}
                        onChange={(e) => updateItem(procedures, setProcedures, 'procedureId', proc.procedureId, 'isRepeatable', e.target.checked)}
                      />
                    </td>
                    <td>
                      <div className="action-buttons">
                        <button type="button" className="primary" onClick={() => saveProcedure(proc)}>Save</button>
                        <button type="button" className="danger" onClick={() => removeItem(procedures, setProcedures, (id) => deleteProviderProcedure(provider.providerId, id), 'procedureId', proc.procedureId)}>Delete</button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {activeSection === 'mappings' && (
        <div className="panel">
          <div className="panel-header">
            <h2>Field mappings</h2>
            <button type="button" className="primary" onClick={addMapping}>+ Add</button>
          </div>
          {mappings.length === 0 ? (
            <p className="empty-state">No field mappings yet. Add a procedure first, then add mappings.</p>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Procedure</th>
                  <th>Parameter</th>
                  <th>XML path</th>
                  <th style={{ width: 80 }}>Required</th>
                  <th>Default</th>
                  <th style={{ width: 130 }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {mappings.map((map, i) => (
                  <tr key={`${map.mappingId}-${i}`}>
                    <td>
                      <select
                        value={map.procedureId || ''}
                        onChange={(e) => updateItem(mappings, setMappings, 'mappingId', map.mappingId, 'procedureId', Number(e.target.value))}
                      >
                        <option value="">Select</option>
                        {procedures.map((p) => (
                          <option key={p.procedureId} value={p.procedureId}>{p.procedureName}</option>
                        ))}
                      </select>
                    </td>
                    <td><input value={map.parameterName || ''} onChange={(e) => updateItem(mappings, setMappings, 'mappingId', map.mappingId, 'parameterName', e.target.value)} /></td>
                    <td><input value={map.xmlPath || ''} onChange={(e) => updateItem(mappings, setMappings, 'mappingId', map.mappingId, 'xmlPath', e.target.value)} /></td>
                    <td style={{ textAlign: 'center' }}>
                      <input type="checkbox" checked={map.isRequired || false} onChange={(e) => updateItem(mappings, setMappings, 'mappingId', map.mappingId, 'isRequired', e.target.checked)} />
                    </td>
                    <td><input value={map.defaultValue || ''} onChange={(e) => updateItem(mappings, setMappings, 'mappingId', map.mappingId, 'defaultValue', e.target.value)} /></td>
                    <td>
                      <div className="action-buttons">
                        <button type="button" className="primary" onClick={() => saveMapping(map)}>Save</button>
                        <button type="button" className="danger" onClick={() => removeItem(mappings, setMappings, (id) => deleteFieldMapping(provider.providerId, id), 'mappingId', map.mappingId)}>Delete</button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {activeSection === 'rules' && (
        <div className="panel">
          <div className="panel-header">
            <h2>Provider rules</h2>
            <button type="button" className="primary" onClick={addRule}>+ Add</button>
          </div>
          {rules.length === 0 ? (
            <p className="empty-state">No rules configured for this provider.</p>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Parameter</th>
                  <th>Rule type</th>
                  <th>Rule value</th>
                  <th>Category</th>
                  <th style={{ width: 70 }}>Active</th>
                  <th style={{ width: 130 }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {rules.map((rule, i) => (
                  <tr key={`${rule.ruleId}-${i}`}>
                    <td><input value={rule.parameterName || ''} onChange={(e) => updateItem(rules, setRules, 'ruleId', rule.ruleId, 'parameterName', e.target.value)} /></td>
                    <td><input value={rule.ruleType || ''} onChange={(e) => updateItem(rules, setRules, 'ruleId', rule.ruleId, 'ruleType', e.target.value)} /></td>
                    <td><input value={rule.ruleValue || ''} onChange={(e) => updateItem(rules, setRules, 'ruleId', rule.ruleId, 'ruleValue', e.target.value)} /></td>
                    <td><input value={rule.ruleCategory || ''} onChange={(e) => updateItem(rules, setRules, 'ruleId', rule.ruleId, 'ruleCategory', e.target.value)} /></td>
                    <td style={{ textAlign: 'center' }}>
                      <input type="checkbox" checked={rule.isActive || false} onChange={(e) => updateItem(rules, setRules, 'ruleId', rule.ruleId, 'isActive', e.target.checked)} />
                    </td>
                    <td>
                      <div className="action-buttons">
                        <button type="button" className="primary" onClick={() => saveRule(rule)}>Save</button>
                        <button type="button" className="danger" onClick={() => removeItem(rules, setRules, (id) => deleteProviderRule(provider.providerId, id), 'ruleId', rule.ruleId)}>Delete</button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {activeSection === 'metadata' && (
        <div className="panel">
          <div className="panel-header">
            <h2>Service metadata</h2>
            <button type="button" className="primary" onClick={addMetadataRow}>+ Add</button>
          </div>
          <table>
            <thead>
              <tr>
                <th>Operator type</th>
                <th>Value</th>
                <th>ORI number</th>
                <th style={{ width: 70 }}>Active</th>
                <th>Created</th>
                <th>Updated</th>
                <th style={{ width: 130 }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {metadataRows.length === 0 && (
                <tr><td colSpan="7" style={{ color: '#9ca3af', padding: '24px 16px', fontStyle: 'italic' }}>No metadata rows found.</td></tr>
              )}
              {metadataRows.map((row) => {
                const key = row.cdServiceMetaDataId || row.tempId;
                return (
                  <tr key={key}>
                    <td><input type="number" value={row.operatorType ?? ''} onChange={(e) => updateMetadataRow(key, 'operatorType', Number(e.target.value))} /></td>
                    <td><input value={row.value ?? ''} onChange={(e) => updateMetadataRow(key, 'value', e.target.value)} /></td>
                    <td><input value={row.orinNum ?? ''} onChange={(e) => updateMetadataRow(key, 'orinNum', e.target.value)} /></td>
                    <td style={{ textAlign: 'center' }}><input type="checkbox" checked={row.isActive ?? false} onChange={(e) => updateMetadataRow(key, 'isActive', e.target.checked)} /></td>
                    <td style={{ color: '#6b7280', fontSize: 12 }}>{row.createdDate ? new Date(row.createdDate).toLocaleDateString() : '—'}</td>
                    <td style={{ color: '#6b7280', fontSize: 12 }}>{row.updatedDate ? new Date(row.updatedDate).toLocaleDateString() : '—'}</td>
                    <td>
                      <div className="action-buttons">
                        <button type="button" className="primary" onClick={() => saveMetadataRow(row)}>Save</button>
                        <button type="button" className="danger" onClick={() => deleteMetadataRow(row)}>Delete</button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {activeSection === 'email' && (
        <div className="panel">
          <EmailSettings application={application} provider={provider} />
        </div>
      )}

      {activeSection === 'retry' && (
        <div className="panel">
          <RetrySettings application={application} provider={provider} />
        </div>
      )}
    </section>
  );
}