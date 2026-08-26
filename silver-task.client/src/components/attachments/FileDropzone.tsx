import { useRef, useState, type ChangeEvent, type DragEvent } from 'react';
import { AlertCircle, UploadCloud, X } from 'lucide-react';
import { ApiError } from '@/api/httpClient';
import type { Attachment } from '@/types/attachment';
import './FileDropzone.css';

interface StagedUpload {
  id: string;
  fileName: string;
  progress: number;
  status: 'uploading' | 'error';
  error?: string;
}

interface FileDropzoneProps {
  /** Bound by the caller to a mutation's mutateAsync (e.g.
   * `(file, onProgress) => uploadTaskAttachment.mutateAsync({ file, onProgress })`) so cache
   * invalidation/activity-log side effects still happen through the normal mutation path — this
   * component only owns the ephemeral per-file progress/error UI state, not server state. */
  onUpload: (file: File, onProgress: (fraction: number) => void) => Promise<Attachment>;
  disabled?: boolean;
  multiple?: boolean;
  label?: string;
  /** Compact single-line variant for Task Detail / Comment composer; the default is the larger
   * Project Files dropzone. */
  compact?: boolean;
}

export function FileDropzone({ onUpload, disabled, multiple = true, label, compact }: FileDropzoneProps) {
  const [isDragging, setIsDragging] = useState(false);
  const [staged, setStaged] = useState<StagedUpload[]>([]);
  const inputRef = useRef<HTMLInputElement>(null);

  function handleFiles(fileList: FileList | null) {
    if (!fileList || fileList.length === 0 || disabled) {
      return;
    }
    const files = multiple ? Array.from(fileList) : [fileList[0]];

    files.forEach((file) => {
      const id = `${Date.now()}-${Math.random().toString(36).slice(2)}`;
      setStaged((prev) => [...prev, { id, fileName: file.name, progress: 0, status: 'uploading' }]);

      onUpload(file, (fraction) => {
        setStaged((prev) => prev.map((s) => (s.id === id ? { ...s, progress: fraction } : s)));
      })
        .then(() => {
          setStaged((prev) => prev.filter((s) => s.id !== id));
        })
        .catch((error: unknown) => {
          const message = error instanceof ApiError ? error.message : 'Upload failed.';
          setStaged((prev) => prev.map((s) => (s.id === id ? { ...s, status: 'error', error: message } : s)));
        });
    });
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    setIsDragging(false);
    if (!disabled) {
      handleFiles(event.dataTransfer.files);
    }
  }

  function handleInputChange(event: ChangeEvent<HTMLInputElement>) {
    handleFiles(event.target.files);
    event.target.value = ''; // allow re-selecting the same file again later
  }

  function dismissStaged(id: string) {
    setStaged((prev) => prev.filter((s) => s.id !== id));
  }

  return (
    <div className={`file-dropzone${compact ? ' file-dropzone--compact' : ''}`}>
      <div
        className={`file-dropzone__area${isDragging ? ' file-dropzone__area--dragging' : ''}${disabled ? ' file-dropzone__area--disabled' : ''}`}
        onClick={() => !disabled && inputRef.current?.click()}
        onDragOver={(e) => {
          e.preventDefault();
          if (!disabled) setIsDragging(true);
        }}
        onDragLeave={() => setIsDragging(false)}
        onDrop={handleDrop}
        role="button"
        tabIndex={disabled ? -1 : 0}
        aria-label="Upload files"
      >
        <UploadCloud size={compact ? 14 : 20} />
        <span>{label ?? (compact ? 'Add File' : 'Drag files here or click to browse')}</span>
      </div>
      <input
        ref={inputRef}
        type="file"
        className="file-dropzone__input"
        multiple={multiple}
        disabled={disabled}
        onChange={handleInputChange}
      />

      {staged.length > 0 && (
        <div className="file-dropzone__staged">
          {staged.map((item) => (
            <div className="file-dropzone__staged-item" key={item.id}>
              <span className="file-dropzone__staged-name">{item.fileName}</span>
              {item.status === 'uploading' ? (
                <div className="file-dropzone__progress">
                  <div className="file-dropzone__progress-bar" style={{ width: `${Math.round(item.progress * 100)}%` }} />
                </div>
              ) : (
                <span className="file-dropzone__staged-error">
                  <AlertCircle size={12} />
                  {item.error}
                </span>
              )}
              <button
                type="button"
                className="file-dropzone__staged-dismiss"
                aria-label={`Dismiss ${item.fileName}`}
                onClick={() => dismissStaged(item.id)}
              >
                <X size={12} />
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
