import { File, FileArchive, FileSpreadsheet, FileText, Image as ImageIcon } from 'lucide-react';
import { categorizeAttachment } from '@/utils/attachmentType';

const ICON_BY_CATEGORY = {
  pdf: FileText,
  image: ImageIcon,
  spreadsheet: FileSpreadsheet,
  document: FileText,
  archive: FileArchive,
  other: File,
};

export function AttachmentIcon({ mimeType, size = 14 }: { mimeType: string; size?: number }) {
  const Icon = ICON_BY_CATEGORY[categorizeAttachment(mimeType)];
  return <Icon size={size} className="attachment-row__icon" />;
}
