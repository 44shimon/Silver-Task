import { useRecurrenceSeries } from '@/hooks/useRecurrence';
import { StatusBadge } from './StatusBadge';
import { formatDate } from '@/utils/formatDate';
import { Modal } from '@/components/shared/Modal';
import '@/components/shared/ConfirmDeleteDialog.css';
import './MoveTaskDialog.css';
import './RecurrenceSeriesDialog.css';

interface RecurrenceSeriesDialogProps {
  taskId: string;
  onOpenDetail: (taskId: string) => void;
  onClose: () => void;
}

// Read-only list of every task this series has generated so far (including the first occurrence)
// — a lightweight way to jump to any occurrence's own detail rather than a second UI for editing
// them here.
export function RecurrenceSeriesDialog({ taskId, onOpenDetail, onClose }: RecurrenceSeriesDialogProps) {
  const { data: series, isLoading } = useRecurrenceSeries(taskId, true);

  return (
    <Modal onClose={onClose}>
      <h2>Recurring Series</h2>

      <div className="recurrence-series-dialog__list">
        {isLoading && <p className="dependencies-section__empty">Loading...</p>}
        {series?.map((occurrence) => (
          <button
            key={occurrence.id}
            type="button"
            className="recurrence-series-dialog__row"
            onClick={() => {
              onOpenDetail(occurrence.id);
              onClose();
            }}
          >
            <span className="recurrence-series-dialog__occurrence">
              #{occurrence.occurrenceNumber ?? '?'}
            </span>
            <span className="recurrence-series-dialog__title">{occurrence.title}</span>
            <span className="recurrence-series-dialog__date">
              {occurrence.recurrenceOccurrenceDate ? formatDate(occurrence.recurrenceOccurrenceDate) : ''}
            </span>
            <StatusBadge status={occurrence.status} />
          </button>
        ))}
      </div>

      <div className="move-task-dialog__actions">
        <button type="button" className="confirm-delete-dialog__cancel" onClick={onClose}>
          Close
        </button>
      </div>
    </Modal>
  );
}
