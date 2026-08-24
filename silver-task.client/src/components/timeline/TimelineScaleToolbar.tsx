import type { TimelineScale } from '@/utils/timelineGrid';

interface TimelineScaleToolbarProps {
  scale: TimelineScale;
  onScaleChange: (scale: TimelineScale) => void;
  onToday: () => void;
}

// Shared by TimelineView and GanttView — same zoom control, same "Today" scroll-to action.
export function TimelineScaleToolbar({ scale, onScaleChange, onToday }: TimelineScaleToolbarProps) {
  return (
    <div className="timeline-view__toolbar">
      <button type="button" className="timeline-view__today-btn" onClick={onToday}>
        Today
      </button>
      <div className="timeline-view__scale-switch" role="tablist">
        {(['day', 'week', 'month'] as const).map((option) => (
          <button
            key={option}
            type="button"
            role="tab"
            aria-selected={scale === option}
            className={`timeline-view__scale-item${scale === option ? ' timeline-view__scale-item--active' : ''}`}
            onClick={() => onScaleChange(option)}
          >
            {option[0].toUpperCase() + option.slice(1)}
          </button>
        ))}
      </div>
    </div>
  );
}
