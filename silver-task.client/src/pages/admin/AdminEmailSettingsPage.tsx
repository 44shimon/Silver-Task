import { useState, type FormEvent } from 'react';
import { CheckCircle2, XCircle } from 'lucide-react';
import {
  useEmailDeliveries,
  useEmailStatus,
  useEmailTemplates,
  usePreviewEmailTemplate,
  useResetEmailTemplate,
  useSendTestEmail,
  useUpsertEmailTemplate,
} from '@/hooks/useAdminEmail';
import type { EmailTemplate, EmailTemplatePreview } from '@/types/email';
import { ApiError } from '@/api/httpClient';
import '../settings/SettingsForm.css';
import './AdminEmailSettingsPage.css';

const TEMPLATE_LABELS: Record<string, string> = {
  TaskAssigned: 'Task Assigned',
  MentionedInComment: 'Mentioned in a Comment',
  TaskDueSoon: 'Task Due Soon',
  TaskOverdue: 'Task Overdue',
  UserAddedToProject: 'Added to a Project',
  DailyDigest: 'Daily Digest',
  WeeklyDigest: 'Weekly Digest',
};

export function AdminEmailSettingsPage() {
  const { data: status } = useEmailStatus();

  return (
    <div className="admin-email-settings">
      <h1>Email</h1>

      <section className="admin-email-settings__section">
        <h2>SMTP Status</h2>
        <p className="admin-email-settings__status">
          {status?.isConfigured ? (
            <>
              <CheckCircle2 size={16} className="admin-email-settings__status-ok" /> Email is configured.
            </>
          ) : (
            <>
              <XCircle size={16} className="admin-email-settings__status-bad" /> Email is not configured — set the Smtp:Host
              connection settings (and related Smtp:* values) via environment variables or user-secrets, never in source control.
            </>
          )}
        </p>
        <TestEmailForm disabled={!status?.isConfigured} />
      </section>

      <section className="admin-email-settings__section">
        <h2>Email Templates</h2>
        <p className="admin-email-settings__hint">
          Customize the subject, heading, body, and button text for these notification emails. Leave a field blank to use the
          built-in default. Notification variables: <code>{'{{UserName}}'}</code>, <code>{'{{ActorName}}'}</code>,{' '}
          <code>{'{{TaskName}}'}</code>, <code>{'{{ProjectName}}'}</code>, <code>{'{{DueDate}}'}</code>,{' '}
          <code>{'{{ActionUrl}}'}</code>. Daily/Weekly Digest variables: <code>{'{{UserName}}'}</code>,{' '}
          <code>{'{{DigestDate}}'}</code>, <code>{'{{AssignmentCount}}'}</code>, <code>{'{{MentionCount}}'}</code>,{' '}
          <code>{'{{CommentCount}}'}</code>, <code>{'{{DueTodayCount}}'}</code>, <code>{'{{OverdueCount}}'}</code>,{' '}
          <code>{'{{DigestContent}}'}</code>, <code>{'{{ActionUrl}}'}</code>.
        </p>
        <EmailTemplateManager />
      </section>

      <section className="admin-email-settings__section">
        <h2>Delivery Log</h2>
        <EmailDeliveryLog />
      </section>
    </div>
  );
}

function TestEmailForm({ disabled }: { disabled: boolean }) {
  const [toEmail, setToEmail] = useState('');
  const sendTest = useSendTestEmail();

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    sendTest.mutate(toEmail);
  }

  return (
    <form className="admin-email-settings__test-form" onSubmit={handleSubmit}>
      <input
        type="email"
        required
        placeholder="you@example.com"
        value={toEmail}
        disabled={disabled}
        onChange={(e) => setToEmail(e.target.value)}
        aria-label="Test email recipient"
      />
      <button type="submit" className="settings-form__save" disabled={disabled || sendTest.isPending}>
        {sendTest.isPending ? 'Sending...' : 'Send Test Email'}
      </button>
      {sendTest.data && (
        <p className={sendTest.data.success ? 'settings-form__success' : 'form-error'}>{sendTest.data.message}</p>
      )}
      {sendTest.isError && (
        <p className="form-error">{sendTest.error instanceof ApiError ? sendTest.error.message : 'Could not send test email.'}</p>
      )}
    </form>
  );
}

function EmailTemplateManager() {
  const { data: templates, isLoading } = useEmailTemplates();
  const [selectedType, setSelectedType] = useState<string | null>(null);

  if (isLoading) {
    return <p>Loading...</p>;
  }
  if (!templates) {
    return <p>Email templates could not be loaded.</p>;
  }

  const selected = templates.find((t) => t.notificationType === selectedType) ?? templates[0] ?? null;

  return (
    <div className="admin-email-settings__templates">
      <ul className="admin-email-settings__template-list">
        {templates.map((template) => (
          <li key={template.notificationType}>
            <button
              type="button"
              className={`admin-email-settings__template-item${selected?.notificationType === template.notificationType ? ' admin-email-settings__template-item--active' : ''}`}
              onClick={() => setSelectedType(template.notificationType)}
            >
              {TEMPLATE_LABELS[template.notificationType] ?? template.notificationType}
              {template.isCustomized && <span className="admin-email-settings__customized-badge">Customized</span>}
            </button>
          </li>
        ))}
      </ul>
      {selected && <EmailTemplateEditor key={selected.notificationType} template={selected} />}
    </div>
  );
}

function EmailTemplateEditor({ template }: { template: EmailTemplate }) {
  const [subjectTemplate, setSubjectTemplate] = useState(template.subjectTemplate ?? '');
  const [headingTemplate, setHeadingTemplate] = useState(template.headingTemplate ?? '');
  const [bodyTemplate, setBodyTemplate] = useState(template.bodyTemplate ?? '');
  const [ctaText, setCtaText] = useState(template.ctaText ?? '');
  const [footerTemplate, setFooterTemplate] = useState(template.footerTemplate ?? '');
  const [preview, setPreview] = useState<EmailTemplatePreview | null>(null);

  const upsert = useUpsertEmailTemplate();
  const reset = useResetEmailTemplate();
  const previewMutation = usePreviewEmailTemplate();

  function handleSave() {
    upsert.mutate({
      notificationType: template.notificationType,
      request: {
        subjectTemplate: subjectTemplate.trim() || null,
        headingTemplate: headingTemplate.trim() || null,
        bodyTemplate: bodyTemplate.trim() || null,
        ctaText: ctaText.trim() || null,
        footerTemplate: footerTemplate.trim() || null,
      },
    });
  }

  function handleReset() {
    if (!window.confirm('Reset this template to its default text? Your customizations will be lost.')) {
      return;
    }
    reset.mutate(template.notificationType, {
      onSuccess: () => {
        setSubjectTemplate('');
        setHeadingTemplate('');
        setBodyTemplate('');
        setCtaText('');
        setFooterTemplate('');
        setPreview(null);
      },
    });
  }

  function handlePreview() {
    previewMutation.mutate(template.notificationType, { onSuccess: (result) => setPreview(result) });
  }

  return (
    <div className="admin-email-settings__editor">
      <div className="settings-form__field">
        <label>Subject</label>
        <input value={subjectTemplate} placeholder={template.defaultSubject} onChange={(e) => setSubjectTemplate(e.target.value)} />
      </div>
      <div className="settings-form__field">
        <label>Heading</label>
        <input value={headingTemplate} placeholder={template.defaultHeading} onChange={(e) => setHeadingTemplate(e.target.value)} />
      </div>
      <div className="settings-form__field">
        <label>Body</label>
        <textarea rows={4} value={bodyTemplate} placeholder={template.defaultBody} onChange={(e) => setBodyTemplate(e.target.value)} />
      </div>
      <div className="settings-form__field">
        <label>Button text</label>
        <input value={ctaText} placeholder={template.defaultCtaText} onChange={(e) => setCtaText(e.target.value)} />
      </div>
      <div className="settings-form__field">
        <label>Additional footer note (optional)</label>
        <input value={footerTemplate} onChange={(e) => setFooterTemplate(e.target.value)} />
      </div>

      {template.updatedByName && (
        <p className="admin-email-settings__hint">
          Last customized by {template.updatedByName} on {template.updatedAt ? new Date(template.updatedAt).toLocaleString() : ''}.
        </p>
      )}

      <div className="admin-email-settings__editor-actions">
        <button type="button" className="settings-form__save" onClick={handleSave} disabled={upsert.isPending}>
          {upsert.isPending ? 'Saving...' : 'Save'}
        </button>
        <button type="button" onClick={handlePreview} disabled={previewMutation.isPending}>
          {previewMutation.isPending ? 'Rendering...' : 'Preview'}
        </button>
        {template.isCustomized && (
          <button type="button" onClick={handleReset} disabled={reset.isPending}>
            Reset to Default
          </button>
        )}
      </div>

      {upsert.isError && (
        <p className="form-error">{upsert.error instanceof ApiError ? upsert.error.message : 'Could not save template.'}</p>
      )}
      {upsert.isSuccess && <p className="settings-form__success">Template saved.</p>}

      {preview && (
        <div className="admin-email-settings__preview">
          <p className="admin-email-settings__preview-subject">Subject: {preview.subject}</p>
          <iframe title="Email preview" className="admin-email-settings__preview-frame" srcDoc={preview.htmlBody} />
        </div>
      )}
    </div>
  );
}

function EmailDeliveryLog() {
  const [page, setPage] = useState(1);
  const pageSize = 25;
  const { data, isLoading } = useEmailDeliveries(page, pageSize);

  if (isLoading) {
    return <p>Loading...</p>;
  }
  if (!data || data.items.length === 0) {
    return <p>No email deliveries yet.</p>;
  }

  const totalPages = Math.max(1, Math.ceil(data.totalCount / pageSize));

  return (
    <div>
      <table className="admin-email-settings__delivery-table">
        <thead>
          <tr>
            <th>Type</th>
            <th>Recipient</th>
            <th>Status</th>
            <th>Attempts</th>
            <th>Queued</th>
            <th>Sent / Failed</th>
            <th>Last error</th>
          </tr>
        </thead>
        <tbody>
          {data.items.map((delivery) => (
            <tr key={delivery.id}>
              <td>{TEMPLATE_LABELS[delivery.notificationType] ?? delivery.notificationType}</td>
              <td>{delivery.recipientName ?? '—'}</td>
              <td>
                <span className={`admin-email-settings__status-badge admin-email-settings__status-badge--${delivery.status.toLowerCase()}`}>
                  {delivery.status}
                </span>
              </td>
              <td>{delivery.attemptCount}</td>
              <td>{new Date(delivery.queuedAt).toLocaleString()}</td>
              <td>{delivery.sentAt ? new Date(delivery.sentAt).toLocaleString() : delivery.failedAt ? new Date(delivery.failedAt).toLocaleString() : '—'}</td>
              <td>{delivery.lastError ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <div className="admin-email-settings__pagination">
        <button type="button" onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1}>
          Previous
        </button>
        <span>
          Page {page} of {totalPages}
        </span>
        <button type="button" onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages}>
          Next
        </button>
      </div>
    </div>
  );
}
