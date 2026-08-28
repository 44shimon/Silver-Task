import { Fragment } from 'react';

interface HighlightedTextProps {
  text: string;
  query: string;
}

/** Renders `text` with every case-insensitive occurrence of `query` wrapped in <mark> — plain
 * React children throughout, never dangerouslySetInnerHTML (spec #16/#84's own explicit "do not
 * inject raw HTML from search results" requirement). A malicious title/comment/filename
 * containing "<script>" is just text split around a match; it's never parsed as markup. */
export function HighlightedText({ text, query }: HighlightedTextProps) {
  const trimmedQuery = query.trim();
  if (!trimmedQuery) {
    return <>{text}</>;
  }

  const lowerText = text.toLowerCase();
  const lowerQuery = trimmedQuery.toLowerCase();
  const parts: { value: string; matched: boolean }[] = [];
  let cursor = 0;

  while (cursor < text.length) {
    const matchIndex = lowerText.indexOf(lowerQuery, cursor);
    if (matchIndex === -1) {
      parts.push({ value: text.slice(cursor), matched: false });
      break;
    }
    if (matchIndex > cursor) {
      parts.push({ value: text.slice(cursor, matchIndex), matched: false });
    }
    parts.push({ value: text.slice(matchIndex, matchIndex + trimmedQuery.length), matched: true });
    cursor = matchIndex + trimmedQuery.length;
  }

  return (
    <>
      {parts.map((part, index) => (
        <Fragment key={index}>{part.matched ? <mark>{part.value}</mark> : part.value}</Fragment>
      ))}
    </>
  );
}
