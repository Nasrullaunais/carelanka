import { useState, type KeyboardEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Button, InputGroup, InputGroupInput, Label, TextField } from '@heroui/react';
import type { AddressSuggestion } from '../../../services/api/generated';
import { searchSceneAddressesOptions } from '../../../services/api/generated/@tanstack/react-query.gen';

const MIN_QUERY_LENGTH = 3;

// Searches only when asked: the free address service forbids search-as-you-type.
export function SceneAddressSearch({ onPick }: { onPick: (suggestion: AddressSuggestion) => void }) {
  const [text, setText] = useState('');
  const [submitted, setSubmitted] = useState('');
  const results = useQuery({
    ...searchSceneAddressesOptions({ query: { query: submitted } }),
    enabled: submitted.length >= MIN_QUERY_LENGTH,
    staleTime: 5 * 60_000,
  });
  const canSearch = text.trim().length >= MIN_QUERY_LENGTH;

  function search() {
    if (canSearch) setSubmitted(text.trim());
  }

  function searchOnEnter(event: KeyboardEvent) {
    if (event.key !== 'Enter') return;
    event.preventDefault();
    search();
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-end gap-2">
        <TextField className="flex-1" value={text} onChange={setText}>
          <Label>Search for the scene address</Label>
          <InputGroup><InputGroupInput placeholder="Street, landmark or town" onKeyDown={searchOnEnter} /></InputGroup>
        </TextField>
        <Button type="button" variant="outline" isDisabled={!canSearch || results.isFetching} onPress={search}>
          {results.isFetching ? 'Searching…' : 'Search'}
        </Button>
      </div>
      {results.isError && <p className="text-sm text-danger">Address search is not working right now. Pick the spot on the map instead.</p>}
      {results.data?.length === 0 && <p className="text-sm text-muted">No places in Sri Lanka match that search.</p>}
      {results.data && results.data.length > 0 && (
        <ul className="flex flex-col gap-1" aria-label="Address matches">
          {results.data.map((suggestion, index) => (
            <li key={`${suggestion.latitude},${suggestion.longitude},${index}`}>
              <Button
                type="button"
                size="sm"
                variant="ghost"
                className="h-auto w-full justify-start whitespace-normal py-1 text-left"
                onPress={() => { onPick(suggestion); setSubmitted(''); setText(suggestion.label ?? text); }}
              >
                {suggestion.label}
                <span className="ml-2 text-xs text-muted">within about {suggestion.approximate_accuracy_metres} m</span>
              </Button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
