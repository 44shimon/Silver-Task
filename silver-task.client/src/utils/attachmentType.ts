import type { AttachmentTypeFilter } from '@/types/attachment';

const SPREADSHEET_TYPES = new Set([
  'text/csv',
  'application/vnd.ms-excel',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
]);

const DOCUMENT_TYPES = new Set([
  'text/plain',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
]);

const ARCHIVE_TYPES = new Set(['application/zip', 'application/x-zip-compressed']);

/** Mirrors AttachmentService.GetAllForProjectAsync's server-side type bucketing exactly — used
 * client-side only for icon selection and the task-attachments compact list (which has no
 * server-side filter of its own to lean on). */
export function categorizeAttachment(mimeType: string): Exclude<AttachmentTypeFilter, 'all'> {
  if (mimeType === 'application/pdf') return 'pdf';
  if (mimeType.startsWith('image/')) return 'image';
  if (SPREADSHEET_TYPES.has(mimeType)) return 'spreadsheet';
  if (DOCUMENT_TYPES.has(mimeType)) return 'document';
  if (ARCHIVE_TYPES.has(mimeType)) return 'archive';
  return 'other';
}

export const ATTACHMENT_TYPE_LABELS: Record<AttachmentTypeFilter, string> = {
  all: 'All types',
  pdf: 'PDF',
  image: 'Image',
  spreadsheet: 'Spreadsheet',
  document: 'Document',
  archive: 'Archive',
  other: 'Other',
};
