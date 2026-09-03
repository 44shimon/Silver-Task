import { useState } from 'react';
import { Modal } from '@/components/shared/Modal';
import { ApiError } from '@/api/httpClient';
import { useRotateApiKey } from '@/hooks/useApiKeys';
import type { ApiKeyCreated, ApiKeySummary } from '@/types/apiKeys';
import './CreateApiKeyDialog.css';

interface RotateApiKeyDialogProps {
  apiKey: ApiKeySummary;
  onClose: () => void;
}

/** Rotation = revoke the existing key and issue a new one (not an in-place secret swap) — see
 * ApiKeyService.RotateApiKeyAsync's own doc comment. The old key stops working the instant this
 * completes, so this asks for confirmation before doing anything, then shows the new key exactly
 * once — same "shown once" box CreateApiKeyDialog uses. */
export function RotateApiKeyDialog({ apiKey, onClose }: RotateApiKeyDialogProps) {
  const rotateApiKey = useRotateApiKey();
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<ApiKeyCreated | null>(null);
  const [copied, setCopied] = useState(false);

  async function handleConfirm() {
    setError(null);
    try {
      const rotated = await rotateApiKey.mutateAsync(apiKey.id);
      setResult(rotated);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not rotate the API key.');
    }
  }

  async function handleCopy() {
    if (!result) return;
    await navigator.clipboard.writeText(result.key);
    setCopied(true);
  }

  if (result) {
    return (
      <Modal onClose={onClose}>
        <h2>API key rotated</h2>
        <p className="create-api-key-dialog__warning">
          The previous key ({apiKey.keyPrefix}...) no longer works. Copy the new key now — it will never be shown
          again.
        </p>
        <div className="create-api-key-dialog__key-box">
          <code>{result.key}</code>
        </div>
        <div className="create-api-key-dialog__actions">
          <button type="button" className="create-api-key-dialog__copy" onClick={handleCopy}>
            {copied ? 'Copied' : 'Copy to clipboard'}
          </button>
          <button type="button" className="create-api-key-dialog__submit" onClick={onClose}>
            Done
          </button>
        </div>
      </Modal>
    );
  }

  return (
    <Modal onClose={onClose}>
      <h2>Rotate API key?</h2>
      <p className="create-api-key-dialog__warning">
        "{apiKey.name}" ({apiKey.keyPrefix}...) will stop working immediately, replaced by a newly generated key.
        Any integration still using the old value will start failing until it's updated.
      </p>

      {error && <p className="form-error">{error}</p>}

      <div className="create-api-key-dialog__actions">
        <button type="button" className="create-api-key-dialog__cancel" onClick={onClose}>
          Cancel
        </button>
        <button type="button" className="create-api-key-dialog__submit" onClick={handleConfirm} disabled={rotateApiKey.isPending}>
          {rotateApiKey.isPending ? 'Rotating...' : 'Rotate Key'}
        </button>
      </div>
    </Modal>
  );
}
