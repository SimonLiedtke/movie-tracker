<script setup lang="ts">
import { onBeforeUnmount, ref } from 'vue';
import { useApi } from '../core/api/api';
import { toApiError } from '../core/api/api-error';
import type { MovieRef, MovieSearchResponse, MovieSearchResult } from '../core/api/models';
import { yearOf } from './dates';
import type { AfterManualAdd } from './movie-picker';

/**
 * Search field to pick a movie: the user's manually added movies and TMDB's results.
 * Picking a TMDB movie imports it first, so the result is always a movie with an ID.
 *
 * - `v-model:selected`: shows the picked movie instead of the search field (for forms).
 * - `@picked` with `:keep-selection="false"`: emits and clears the field (for "add" actions).
 */
const props = withDefaults(
  defineProps<{
    keepSelection?: boolean;
    afterManualAdd?: AfterManualAdd;
    inputId?: string;
    invalid?: boolean;
    placeholder?: string;
  }>(),
  { keepSelection: true, invalid: false, placeholder: 'Search a movie by title' },
);

const emit = defineEmits<{
  picked: [movie: MovieRef];
}>();

const selected = defineModel<MovieRef | null>('selected', { default: null });

const SEARCH_DEBOUNCE_MS = 250;

const api = useApi();

const term = ref('');
const response = ref<MovieSearchResponse | null>(null);
const searching = ref(false);
const importing = ref(false);
const importError = ref<string | null>(null);
const open = ref(false);

let timer: ReturnType<typeof setTimeout> | undefined;
let lastSearched = '';
/** Numbers the searches, so that the answer to an outdated one is ignored. */
let latestSearch = 0;

onBeforeUnmount(() => {
  clearTimeout(timer);
  latestSearch++;
});

function search(value: string): void {
  term.value = value;
  importError.value = null;
  clearTimeout(timer);
  timer = setTimeout(() => void runSearch(value.trim()), SEARCH_DEBOUNCE_MS);
}

async function runSearch(searchTerm: string): Promise<void> {
  if (searchTerm === lastSearched) {
    return;
  }
  lastSearched = searchTerm;
  const thisSearch = ++latestSearch;

  if (!searchTerm) {
    searching.value = false;
    response.value = null;
    open.value = false;
    return;
  }

  searching.value = true;
  let result: MovieSearchResponse;
  try {
    result = await api.movies.search(searchTerm);
  } catch {
    result = { items: [], tmdbAvailable: false };
  }
  if (thisSearch !== latestSearch) {
    return;
  }
  searching.value = false;
  response.value = result;
  open.value = true;
}

function pick(result: MovieSearchResult): void {
  open.value = false;
  if (result.movieId) {
    done({
      id: result.movieId,
      title: result.title,
      releaseDate: result.releaseDate,
      posterUrl: result.posterUrl,
    });
    return;
  }

  importing.value = true;
  importError.value = null;
  api.movies.importFromTmdb(result.tmdbId!).then(
    (movie) => {
      importing.value = false;
      done(movie);
    },
    (error: unknown) => {
      importing.value = false;
      importError.value = toApiError(error).message;
    },
  );
}

function clear(): void {
  selected.value = null;
  reset();
}

function label(movie: { title: string; releaseDate: string | null }): string {
  const year = yearOf(movie.releaseDate);
  return year ? `${movie.title} (${year})` : movie.title;
}

function done(movie: MovieRef): void {
  const ref: MovieRef = {
    id: movie.id,
    title: movie.title,
    releaseDate: movie.releaseDate,
    posterUrl: movie.posterUrl,
  };
  emit('picked', ref);
  if (props.keepSelection) {
    selected.value = ref;
  }
  reset();
}

function reset(): void {
  clearTimeout(timer);
  latestSearch++;
  lastSearched = '';
  term.value = '';
  response.value = null;
  searching.value = false;
}
</script>

<template>
  <div v-if="keepSelection && selected" class="field has-addons">
    <div class="control is-expanded">
      <input class="input is-static" readonly :value="label(selected)" :id="inputId" />
    </div>
    <div class="control">
      <button type="button" class="button" @click="clear">Change</button>
    </div>
  </div>
  <template v-else>
    <div class="dropdown is-block" :class="{ 'is-active': open }">
      <div class="dropdown-trigger">
        <div class="control" :class="{ 'is-loading': searching || importing }">
          <input
            class="input"
            type="search"
            autocomplete="off"
            :placeholder="placeholder"
            :id="inputId"
            :class="{ 'is-danger': invalid }"
            :value="term"
            :disabled="importing"
            @input="search(($event.target as HTMLInputElement).value)"
            @focus="open = response !== null"
            @keydown.escape="open = false"
          />
        </div>
      </div>
      <div class="dropdown-menu" role="menu" style="width: 100%">
        <div class="dropdown-content" style="max-height: 24rem; overflow-y: auto">
          <template v-if="response">
            <div v-if="!response.tmdbAvailable" class="dropdown-item has-text-grey is-size-7">
              TMDB search is not available right now, only your own movies are shown.
            </div>
            <template v-if="response.items.length">
              <a
                v-for="result in response.items"
                :key="result.movieId ?? 'tmdb-' + result.tmdbId"
                class="dropdown-item is-flex is-align-items-center"
                href=""
                @click.prevent="pick(result)"
              >
                <span class="is-flex-grow-1">{{ label(result) }}</span>
                <span
                  v-if="result.tmdbId === null"
                  class="tag is-warning is-light ml-2"
                  title="Added manually by you"
                  >Yours</span
                >
              </a>
            </template>
            <div v-else class="dropdown-item has-text-grey">No matching movies.</div>
            <hr class="dropdown-divider" />
            <RouterLink
              class="dropdown-item"
              :to="{
                path: '/movies/new',
                query: { title: term.trim() || undefined, next: afterManualAdd },
              }"
            >
              Can't find it? <strong>Add it manually</strong>
            </RouterLink>
          </template>
        </div>
      </div>
    </div>
    <p v-if="importError" class="help is-danger">{{ importError }}</p>
  </template>
</template>
