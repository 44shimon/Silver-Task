export const API_BASE = '/api';

export class ApiError extends Error {
  status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

interface ApiErrorBody {
  message?: string;
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  // A FormData body (file upload) must NOT get a manual Content-Type — the browser sets
  // its own multipart boundary automatically, which we'd otherwise clobber.
  const isFormData = options.body instanceof FormData;

  const response = await fetch(`${API_BASE}${path}`, {
    ...options,
    credentials: 'include',
    headers: isFormData
      ? { ...options.headers }
      : { 'Content-Type': 'application/json', ...options.headers },
  });

  if (!response.ok) {
    let message = `Request failed with status ${response.status}`;
    try {
      const body = (await response.json()) as ApiErrorBody;
      if (body.message) {
        message = body.message;
      }
    } catch {
      // Response had no JSON body; fall back to the generic message above.
    }
    throw new ApiError(message, response.status);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/** XHR-based (not fetch) purely so `upload.onprogress` is available — fetch has no portable
 * upload-progress event. Used for file uploads where a per-file progress bar matters; every
 * other request in the app goes through `request()` above. */
function uploadWithProgress<T>(path: string, formData: FormData, onProgress?: (fraction: number) => void): Promise<T> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open('POST', `${API_BASE}${path}`);
    xhr.withCredentials = true;

    xhr.upload.addEventListener('progress', (event) => {
      if (event.lengthComputable && onProgress) {
        onProgress(event.loaded / event.total);
      }
    });

    xhr.addEventListener('load', () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        resolve(xhr.status === 204 || !xhr.responseText ? (undefined as T) : (JSON.parse(xhr.responseText) as T));
        return;
      }
      let message = `Request failed with status ${xhr.status}`;
      try {
        const body = JSON.parse(xhr.responseText) as ApiErrorBody;
        if (body.message) {
          message = body.message;
        }
      } catch {
        // No JSON body — fall back to the generic message above.
      }
      reject(new ApiError(message, xhr.status));
    });

    xhr.addEventListener('error', () => reject(new ApiError('Network error during upload.', 0)));
    xhr.send(formData);
  });
}

export const httpClient = {
  get: <T>(path: string) => request<T>(path, { method: 'GET' }),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body !== undefined ? JSON.stringify(body) : undefined }),
  put: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'PUT', body: body !== undefined ? JSON.stringify(body) : undefined }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
  upload: <T>(path: string, formData: FormData) => request<T>(path, { method: 'POST', body: formData }),
  uploadWithProgress,
};
