import { useRef, useState, type PointerEvent as ReactPointerEvent } from 'react';
import { STATUS_LABELS, type Task } from '@/types/task';
import './TimelineBar.css';

type DragMode = 'move' | 'resize-left' | 'resize-right';

interface TimelineBarProps {
  task: Task;
  left: number;
  width: number;
  top: number;
  height: number;
  pixelsPerDay: number;
  hasError: boolean;
  onOpenDetail: () => void;
  onDragEnd: (mode: DragMode, deltaDays: number) => void;
}

// Pointer events (not HTML5 drag-and-drop) — move/resize need continuous position feedback
// while dragging, which native DnD's dragover-only callbacks don't give cleanly. No new
// dependency: this is the same technique dedicated Gantt libraries use under the hood.
export function TimelineBar({ task, left, width, top, height, pixelsPerDay, hasError, onOpenDetail, onDragEnd }: TimelineBarProps) {
  const [drag, setDrag] = useState<{ mode: DragMode; deltaDays: number } | null>(null);
  const didDragRef = useRef(false);

  function beginDrag(mode: DragMode) {
    return (event: ReactPointerEvent<HTMLDivElement>) => {
      event.preventDefault();
      event.stopPropagation();
      const startX = event.clientX;
      didDragRef.current = false;
      setDrag({ mode, deltaDays: 0 });

      function handleMove(moveEvent: PointerEvent) {
        const deltaDays = Math.round((moveEvent.clientX - startX) / pixelsPerDay);
        if (deltaDays !== 0) {
          didDragRef.current = true;
        }
        setDrag({ mode, deltaDays });
      }

      function handleUp(upEvent: PointerEvent) {
        document.removeEventListener('pointermove', handleMove);
        document.removeEventListener('pointerup', handleUp);
        const deltaDays = Math.round((upEvent.clientX - startX) / pixelsPerDay);
        setDrag(null);
        if (deltaDays !== 0) {
          onDragEnd(mode, deltaDays);
        }
      }

      document.addEventListener('pointermove', handleMove);
      document.addEventListener('pointerup', handleUp);
    };
  }

  function handleClick() {
    if (didDragRef.current) {
      didDragRef.current = false;
      return;
    }
    onOpenDetail();
  }

  let displayLeft = left;
  let displayWidth = width;
  if (drag) {
    const deltaPx = drag.deltaDays * pixelsPerDay;
    if (drag.mode === 'move') {
      displayLeft = left + deltaPx;
    } else if (drag.mode === 'resize-left') {
      displayLeft = left + deltaPx;
      displayWidth = Math.max(pixelsPerDay, width - deltaPx);
    } else {
      displayWidth = Math.max(pixelsPerDay, width + deltaPx);
    }
  }

  return (
    <div
      className={`timeline-bar timeline-bar--priority-${task.priority.toLowerCase()}${drag ? ' timeline-bar--dragging' : ''}${hasError ? ' timeline-bar--error' : ''}`}
      style={{ left: displayLeft, width: displayWidth, top, height }}
      onPointerDown={beginDrag('move')}
      onClick={handleClick}
      title={
        hasError
          ? 'Could not save — try dragging again'
          : `${task.title} — ${STATUS_LABELS[task.status]}, ${task.priority}${task.assignedTo ? `, assigned to ${task.assignedTo.name}` : ''}`
      }
    >
      <div className="timeline-bar__handle timeline-bar__handle--left" onPointerDown={beginDrag('resize-left')} />
      <span className="timeline-bar__label">{task.title}</span>
      <div className="timeline-bar__handle timeline-bar__handle--right" onPointerDown={beginDrag('resize-right')} />
    </div>
  );
}
