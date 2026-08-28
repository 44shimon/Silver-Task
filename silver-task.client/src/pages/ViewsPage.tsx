import { useNavigate } from 'react-router-dom';
import { Copy, Globe, Layers, Plus, Star, Trash2 } from 'lucide-react';
import { useCurrentUser } from '@/hooks/useAuth';
import { useDeleteSavedView, useDuplicateSavedView, useSavedViews, useToggleSavedViewFavorite } from '@/hooks/useSavedViews';
import type { SavedView } from '@/types/savedView';
import './ViewsPage.css';

/** Phase 43 — the Saved Views landing page (spec's own "View sidebar"/browse mockup, rendered
 * here as a full page rather than only a sidebar strip since a user may have many views). Groups
 * views the same way the spec's own list implies: system defaults, favorites, then everything
 * else the caller can see (own/shared/public) — exactly what ISavedViewService.ListForCallerAsync
 * already returns in one call, no per-project fan-out. */
export function ViewsPage() {
  const { data: views, isLoading } = useSavedViews();
  const { data: currentUser } = useCurrentUser();
  const navigate = useNavigate();
  const toggleFavorite = useToggleSavedViewFavorite();
  const duplicate = useDuplicateSavedView();
  const remove = useDeleteSavedView();

  if (isLoading || !views) {
    return <p>Loading views...</p>;
  }

  const systemDefaults = views.filter((v) => v.isSystemDefault);
  const favorites = views.filter((v) => !v.isSystemDefault && v.isFavorite);
  const mine = views.filter((v) => !v.isSystemDefault && !v.isFavorite && v.isOwnedByMe);
  const others = views.filter((v) => !v.isSystemDefault && !v.isFavorite && !v.isOwnedByMe);

  async function handleDuplicate(id: string) {
    const copy = await duplicate.mutateAsync(id);
    navigate(`/views/${copy.id}`);
  }

  function handleDelete(view: SavedView) {
    if (window.confirm('Delete this saved view? This will not delete any tasks or projects.')) {
      remove.mutate(view.id);
    }
  }

  return (
    <div className="views-page">
      <div className="views-page__header">
        <h1>Saved Views</h1>
        <button type="button" className="views-page__new" onClick={() => navigate('/views/new')}>
          <Plus size={14} /> New View
        </button>
      </div>

      <ViewSection title="Defaults" views={systemDefaults} onOpen={(id) => navigate(`/views/${id}`)} onToggleFavorite={toggleFavorite.mutate} onDuplicate={handleDuplicate} onDelete={handleDelete} />
      <ViewSection title="Favorites" views={favorites} onOpen={(id) => navigate(`/views/${id}`)} onToggleFavorite={toggleFavorite.mutate} onDuplicate={handleDuplicate} onDelete={handleDelete} />
      <ViewSection title="My Views" views={mine} onOpen={(id) => navigate(`/views/${id}`)} onToggleFavorite={toggleFavorite.mutate} onDuplicate={handleDuplicate} onDelete={handleDelete} />
      <ViewSection
        title="Shared With Me & Public"
        views={others}
        onOpen={(id) => navigate(`/views/${id}`)}
        onToggleFavorite={toggleFavorite.mutate}
        onDuplicate={handleDuplicate}
        onDelete={handleDelete}
        currentUserId={currentUser?.id}
      />
    </div>
  );
}

function ViewSection({
  title,
  views,
  onOpen,
  onToggleFavorite,
  onDuplicate,
  onDelete,
  currentUserId,
}: {
  title: string;
  views: SavedView[];
  onOpen: (id: string) => void;
  onToggleFavorite: (args: { id: string; favorite: boolean }) => void;
  onDuplicate: (id: string) => void;
  onDelete: (view: SavedView) => void;
  currentUserId?: string;
}) {
  if (views.length === 0) {
    return null;
  }

  return (
    <section className="views-page__section">
      <h2>{title}</h2>
      <ul className="views-page__list">
        {views.map((view) => (
          <li key={view.id} className="views-page__card">
            <button type="button" className="views-page__card-main" onClick={() => onOpen(view.id)}>
              <div className="views-page__card-title">
                <Layers size={14} aria-hidden="true" />
                <span>{view.name}</span>
                {view.isPublic && <Globe size={12} aria-hidden="true" />}
              </div>
              {view.description && <p className="views-page__card-description">{view.description}</p>}
              <span className="views-page__card-meta">
                {view.entityType} · {view.layout}
                {!view.isSystemDefault && view.createdByUserId !== currentUserId && ` · by ${view.createdByName}`}
              </span>
            </button>
            <div className="views-page__card-actions">
              <button
                type="button"
                className="icon-button"
                aria-label={view.isFavorite ? 'Remove from favorites' : 'Add to favorites'}
                onClick={() => onToggleFavorite({ id: view.id, favorite: !view.isFavorite })}
              >
                <Star size={14} fill={view.isFavorite ? 'currentColor' : 'none'} />
              </button>
              <button type="button" className="icon-button" aria-label="Duplicate view" onClick={() => onDuplicate(view.id)}>
                <Copy size={14} />
              </button>
              {!view.isSystemDefault && view.isOwnedByMe && (
                <button type="button" className="icon-button" aria-label="Delete view" onClick={() => onDelete(view)}>
                  <Trash2 size={14} />
                </button>
              )}
            </div>
          </li>
        ))}
      </ul>
    </section>
  );
}
