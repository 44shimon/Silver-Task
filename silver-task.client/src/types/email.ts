/** Mirrors Models/DTOs/Email/EmailTemplateDto.cs. DefaultSubject/DefaultHeading/DefaultBody/
 * DefaultCtaText are the built-in copy (Common/DefaultEmailTemplates.cs) — shown as placeholder
 * text for any field the admin hasn't overridden; the *Template/CtaText fields are the actual
 * override values (null = not customized for that field). */
export interface EmailTemplate {
  notificationType: string;
  subjectTemplate: string | null;
  headingTemplate: string | null;
  bodyTemplate: string | null;
  ctaText: string | null;
  footerTemplate: string | null;
  defaultSubject: string;
  defaultHeading: string;
  defaultBody: string;
  defaultCtaText: string;
  isCustomized: boolean;
  updatedAt: string | null;
  updatedByName: string | null;
}

export interface UpsertEmailTemplateRequest {
  subjectTemplate: string | null;
  headingTemplate: string | null;
  bodyTemplate: string | null;
  ctaText: string | null;
  footerTemplate: string | null;
}

export interface EmailTemplatePreview {
  subject: string;
  htmlBody: string;
}

export interface TestEmailResult {
  success: boolean;
  message: string;
}

export type EmailDeliveryStatus = 'Queued' | 'Sending' | 'Sent' | 'Failed' | 'Cancelled';

export interface EmailDelivery {
  id: string;
  notificationType: string;
  recipientUserId: string;
  recipientName: string | null;
  status: EmailDeliveryStatus;
  attemptCount: number;
  lastError: string | null;
  queuedAt: string;
  sentAt: string | null;
  failedAt: string | null;
}

export interface EmailDeliveryPage {
  items: EmailDelivery[];
  totalCount: number;
  page: number;
  pageSize: number;
}
