<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import { useRouter, type RouteLocationRaw } from 'vue-router';
import { useApi } from '../../core/api/api';
import { toApiError, type ApiError } from '../../core/api/api-error';
import type {
  Genre,
  GenreMatch,
  MovieRef,
  WatchlistItem,
  WatchlistPage,
  WatchlistQuery,
} from '../../core/api/models';
import { formatDate, yearOf } from '../../shared/dates';
import ErrorNotification from '../../shared/ErrorNotification.vue';
import MoviePicker from '../../shared/MoviePicker.vue';
import Pagination from '../../shared/Pagination.vue';
import Poster from '../../shared/Poster.vue';
import { mergeQuery } from '../../shared/query';
import TagList from '../../shared/TagList.vue';
import { formatTags, parseTags, tagsError } from '../../shared/tags';

const PAGE_SIZE = 20;
const SEARCH_DEBOUNCE_MS = 300;

const MATCH_OPTIONS: { value: GenreMatch; label: string }[] = [
  { value: 'any', label: 'Any of them' },
  { value: 'all', label: 'All of them' },
];

/**
 * Quick picks for the runtime, so that the usual questions need one click. They do not overlap:
 * "Under 90 min" is up to 89 minutes, "Over 120 min" from 121.
 */
const RUNTIME_PRESETS: { label: string; from?: number; to?: number }[] = [
  { label: 'Under 90 min', to: 89 },
  { label: '90 to 120 min', from: 90, to: 120 },
  { label: 'Over 120 min', from: 121 },
];

/** The filter and the page, from the route's query parameters. */
const props = defineProps<{
  genreId?: string[];
  genreMatch?: string;
  releasedFrom?: string;
  releasedTo?: string;
  addedFrom?: string;
  addedTo?: string;
  runtimeFrom?: string;
  runtimeTo?: string;
  search?: string;
  tag?: string;
  page?: string;
}>();

const router = useRouter();
const api = useApi();

/** A whole number of minutes; anything else (e.g. from a hand-edited URL) is left out. */
const minutes = (value: string | undefined) => {
  const number = Number(value);
  return Number.isInteger(number) && number > 0 ? number : undefined;
};

const query = computed<WatchlistQuery>(() => ({
  // Whatever is not a genre ID (e.g. from a hand-edited URL) is left out.
  genreIds: [...new Set((props.genreId ?? []).map(Number).filter(Number.isInteger))],
  genreMatch: props.genreMatch === 'all' ? 'all' : 'any',
  releasedFrom: props.releasedFrom || undefined,
  releasedTo: props.releasedTo || undefined,
  addedFrom: props.addedFrom || undefined,
  addedTo: props.addedTo || undefined,
  runtimeFrom: minutes(props.runtimeFrom),
  runtimeTo: minutes(props.runtimeTo),
  // Not trimmed: the field shows the URL's value, and trimming would eat spaces while typing.
  search: props.search?.trim() ? props.search : undefined,
  tag: props.tag?.trim() || undefined,
  page: Number(props.page) || 1,
  pageSize: PAGE_SIZE,
}));
const hasGenres = computed(() => (query.value.genreIds?.length ?? 0) > 0);
const filtered = computed(
  () =>
    hasGenres.value ||
    !!query.value.releasedFrom ||
    !!query.value.releasedTo ||
    !!query.value.addedFrom ||
    !!query.value.addedTo ||
    !!query.value.runtimeFrom ||
    !!query.value.runtimeTo ||
    !!query.value.search ||
    !!query.value.tag,
);

/** E.g. "released from May 25, 1979 to November 3, 1989" or "added until ..."; undefined without dates. */
function describePeriod(what: string, from?: string, to?: string): string | undefined {
  if (from && to) {
    return `${what} from ${formatDate(from)} to ${formatDate(to)}`;
  }
  if (from) {
    return `${what} from ${formatDate(from)}`;
  }
  if (to) {
    return `${what} until ${formatDate(to)}`;
  }
  return undefined;
}

/** E.g. "runtime of at least 90 minutes"; undefined without runtime. */
function describeRuntime(from?: number, to?: number): string | undefined {
  if (from && to) {
    return `runtime of ${from} to ${to} minutes`;
  }
  if (from) {
    return `runtime of at least ${from} minutes`;
  }
  if (to) {
    return `runtime of at most ${to} minutes`;
  }
  return undefined;
}

/** What the filter selects, e.g. "with any of the selected genres, released from May 25, 1979". */
const filterDescription = computed(() => {
  const {
    genreMatch,
    releasedFrom,
    releasedTo,
    addedFrom,
    addedTo,
    runtimeFrom,
    runtimeTo,
    search,
    tag,
  } = query.value;
  const genreText = hasGenres.value
    ? `with ${genreMatch === 'all' ? 'all' : 'any'} of the selected genres`
    : undefined;
  return [
    search ? `matching "${search.trim()}"` : undefined,
    tag ? `tagged "${tag}"` : undefined,
    genreText,
    describeRuntime(runtimeFrom, runtimeTo),
    describePeriod('released', releasedFrom, releasedTo),
    describePeriod('added', addedFrom, addedTo),
  ]
    .filter((part) => part !== undefined)
    .join(', ');
});

const genres = ref<Genre[]>([]);
api.genres.list().then(
  (loaded) => (genres.value = loaded),
  () => undefined, // Without genres, the filter just has no choices.
);

const result = ref<WatchlistPage | null>(null);
const loading = ref(true);
const error = ref<ApiError | null>(null);
/** Movie ID of the item whose note and tags are being edited. */
const editing = ref<string | null>(null);
const noteDraft = ref('');
const tagsDraft = ref('');
/** Why the tags that are being edited are refused; null if they are fine. */
const tagsProblem = ref<string | null>(null);
const saving = ref(false);

/** Numbers the requests, so that the answer to an outdated one is ignored. */
let latestLoad = 0;

/** Loads the current page of the watchlist. */
async function load(): Promise<void> {
  const thisLoad = ++latestLoad;
  loading.value = true;
  let loaded: WatchlistPage | null;
  try {
    loaded = await api.watchlist.list(query.value);
    if (thisLoad !== latestLoad) {
      return;
    }
    error.value = null;
  } catch (failure) {
    if (thisLoad !== latestLoad) {
      return;
    }
    error.value = toApiError(failure);
    loaded = null;
  }

  // E.g. after removing the only item on the last page.
  if (loaded && loaded.items.length === 0 && loaded.page > 1) {
    goToPage(Math.max(1, Math.ceil(loaded.totalCount / loaded.pageSize)));
    return;
  }
  result.value = loaded;
  loading.value = false;
}

/**
 * What the request depends on. The match mode means nothing without genres, and spaces around the
 * search text do not matter, so changing them must not load the same list again.
 */
const requestKey = computed(() =>
  JSON.stringify({
    ...query.value,
    genreMatch: hasGenres.value ? query.value.genreMatch : null,
    search: query.value.search?.trim(),
  }),
);

watch(requestKey, load, { immediate: true });

function goToPage(target: number): void {
  router.push({ query: mergeQuery(router.currentRoute.value.query, { page: target }) });
}

/** Selects the genre, or deselects it if it is selected. Changing the filter starts again at page 1. */
function toggleGenre(genre: Genre): void {
  const selected = query.value.genreIds ?? [];
  const genreIds = selected.includes(genre.id)
    ? selected.filter((id) => id !== genre.id)
    : [...selected, genre.id];
  updateFilter({ genreId: genreIds });
}

function setGenreMatch(match: GenreMatch): void {
  // "any" is the default, so it needs no parameter.
  updateFilter({ genreMatch: match === 'all' ? 'all' : undefined });
}

/** Where a genre tag leads: the watchlist filtered by this genre only, from page 1. */
function genreFilterLink(genre: Genre): RouteLocationRaw {
  return {
    query: mergeQuery(router.currentRoute.value.query, { genreId: [genre.id], page: undefined }),
  };
}

function onTag(value: string): void {
  updateFilter({ tag: value.trim() || undefined });
}

function onRuntime(key: 'runtimeFrom' | 'runtimeTo', value: string): void {
  updateFilter({ [key]: value || undefined });
}

function isRuntimePreset(preset: (typeof RUNTIME_PRESETS)[number]): boolean {
  return query.value.runtimeFrom === preset.from && query.value.runtimeTo === preset.to;
}

/** Sets the runtime to the quick pick, or removes it if it is selected already. */
function toggleRuntimePreset(preset: (typeof RUNTIME_PRESETS)[number]): void {
  const selected = isRuntimePreset(preset);
  updateFilter({
    runtimeFrom: selected ? undefined : preset.from,
    runtimeTo: selected ? undefined : preset.to,
  });
}

function onDate(key: 'releasedFrom' | 'releasedTo' | 'addedFrom' | 'addedTo', value: string): void {
  updateFilter({ [key]: value || undefined });
}

function clearFilter(): void {
  updateFilter({
    genreId: [],
    genreMatch: undefined,
    releasedFrom: undefined,
    releasedTo: undefined,
    addedFrom: undefined,
    addedTo: undefined,
    runtimeFrom: undefined,
    runtimeTo: undefined,
    search: undefined,
    tag: undefined,
  });
}

function updateFilter(changes: Parameters<typeof mergeQuery>[1], replace = false): void {
  const merged = mergeQuery(router.currentRoute.value.query, { ...changes, page: undefined });
  if (replace) {
    router.replace({ query: merged });
  } else {
    router.push({ query: merged });
  }
}

// The field shows the URL's value, but keeps what the user types until the URL has caught up.
const searchText = ref(query.value.search ?? '');
watch(
  () => query.value.search,
  (search) => (searchText.value = search ?? ''),
);

let searchTimer: ReturnType<typeof setTimeout> | undefined;

onBeforeUnmount(() => clearTimeout(searchTimer));

function onSearch(value: string): void {
  searchText.value = value;
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    if (value !== (props.search ?? '')) {
      updateFilter({ search: value.trim() ? value : undefined }, true);
    }
  }, SEARCH_DEBOUNCE_MS);
}

/** Adds the movie at the top of the list (the newest come first). Already on it: nothing changes. */
async function add(movie: MovieRef): Promise<void> {
  try {
    // PUT replaces the note, so a movie that is already on the watchlist is left alone.
    const details = await api.movies.get(movie.id);
    if (!details.onWatchlist) {
      await api.watchlist.put(movie.id, null);
    }
  } catch (failure) {
    error.value = toApiError(failure);
    return;
  }
  if (result.value?.page === 1) {
    void load();
  } else {
    goToPage(1);
  }
}

function edit(item: WatchlistItem): void {
  noteDraft.value = item.note ?? '';
  tagsDraft.value = formatTags(item.tags);
  tagsProblem.value = null;
  editing.value = item.movie.id;
}

/** Saves the note and the tags: the server replaces both, so the one that is not edited is sent along. */
async function saveDetails(item: WatchlistItem): Promise<void> {
  tagsProblem.value = tagsError(tagsDraft.value);
  if (tagsProblem.value) {
    return;
  }
  saving.value = true;
  let updated: WatchlistItem;
  try {
    updated = await api.watchlist.put(
      item.movie.id,
      noteDraft.value.trim() || null,
      parseTags(tagsDraft.value),
    );
  } catch (failure) {
    saving.value = false;
    error.value = toApiError(failure);
    return;
  }
  saving.value = false;
  editing.value = null;
  if (result.value) {
    result.value = {
      ...result.value,
      items: result.value.items.map((i) => (i.movie.id === updated.movie.id ? updated : i)),
    };
  }
}

async function remove(item: WatchlistItem): Promise<void> {
  try {
    await api.watchlist.remove(item.movie.id);
  } catch (failure) {
    error.value = toApiError(failure);
    return;
  }
  void load();
}
</script>

<template>
  <h1 class="title">Watchlist</h1>

  <div class="field">
    <label class="label" for="add">Add a movie</label>
    <MoviePicker
      input-id="add"
      placeholder="Search a movie to watch later"
      after-manual-add="watchlist"
      :keep-selection="false"
      @picked="add"
    />
  </div>

  <div class="box">
    <div class="field">
      <label class="label" for="search">Title</label>
      <div class="control">
        <input
          id="search"
          class="input is-small"
          type="search"
          placeholder="Title or original title"
          :value="searchText"
          @input="onSearch(($event.target as HTMLInputElement).value)"
        />
      </div>
    </div>
    <div class="field">
      <label class="label" for="tag">Tag</label>
      <div class="control">
        <input
          id="tag"
          class="input is-small"
          type="text"
          autocomplete="off"
          placeholder="e.g. weekend"
          :value="query.tag ?? ''"
          @change="onTag(($event.target as HTMLInputElement).value)"
        />
      </div>
    </div>
    <div class="field">
      <label class="label" id="genre-label">Genres</label>
      <div class="buttons are-small" role="group" aria-labelledby="genre-label">
        <button
          v-for="genre in genres"
          :key="genre.id"
          type="button"
          class="button"
          :class="{ 'is-link': query.genreIds?.includes(genre.id) }"
          :aria-pressed="query.genreIds?.includes(genre.id)"
          @click="toggleGenre(genre)"
        >
          {{ genre.name }}
        </button>
      </div>
    </div>
    <div class="field is-flex is-flex-wrap-wrap is-align-items-center">
      <span class="mr-3" id="genre-match-label">Show movies with</span>
      <div
        class="buttons has-addons are-small mb-0"
        role="group"
        aria-labelledby="genre-match-label"
      >
        <button
          v-for="option in MATCH_OPTIONS"
          :key="option.value"
          type="button"
          class="button mb-0"
          :class="{ 'is-link is-selected': query.genreMatch === option.value }"
          :aria-pressed="query.genreMatch === option.value"
          @click="setGenreMatch(option.value)"
        >
          {{ option.label }}
        </button>
      </div>
    </div>
    <div class="field">
      <label class="label" id="runtime-label">Runtime</label>
      <div class="buttons are-small mb-0" role="group" aria-labelledby="runtime-label">
        <button
          v-for="preset in RUNTIME_PRESETS"
          :key="preset.label"
          type="button"
          class="button"
          :class="{ 'is-link': isRuntimePreset(preset) }"
          :aria-pressed="isRuntimePreset(preset)"
          @click="toggleRuntimePreset(preset)"
        >
          {{ preset.label }}
        </button>
      </div>
    </div>
    <div class="columns is-mobile is-multiline is-align-items-flex-end mb-0">
      <div class="column is-half-mobile is-3-tablet">
        <div class="field">
          <label class="label" for="runtime-from">Runtime from (min)</label>
          <div class="control">
            <input
              id="runtime-from"
              class="input is-small"
              type="number"
              min="1"
              max="1500"
              step="1"
              placeholder="Any"
              :value="query.runtimeFrom ?? ''"
              @change="onRuntime('runtimeFrom', ($event.target as HTMLInputElement).value)"
            />
          </div>
        </div>
      </div>
      <div class="column is-half-mobile is-3-tablet">
        <div class="field">
          <label class="label" for="runtime-to">Runtime until (min)</label>
          <div class="control">
            <input
              id="runtime-to"
              class="input is-small"
              type="number"
              min="1"
              max="1500"
              step="1"
              placeholder="Any"
              :value="query.runtimeTo ?? ''"
              @change="onRuntime('runtimeTo', ($event.target as HTMLInputElement).value)"
            />
          </div>
        </div>
      </div>
      <div class="column is-half-mobile is-3-tablet">
        <div class="field">
          <label class="label" for="released-from">Released from</label>
          <div class="control">
            <input
              id="released-from"
              class="input is-small"
              type="date"
              :value="query.releasedFrom ?? ''"
              :max="query.releasedTo ?? ''"
              @change="onDate('releasedFrom', ($event.target as HTMLInputElement).value)"
            />
          </div>
        </div>
      </div>
      <div class="column is-half-mobile is-3-tablet">
        <div class="field">
          <label class="label" for="released-to">Released until</label>
          <div class="control">
            <input
              id="released-to"
              class="input is-small"
              type="date"
              :value="query.releasedTo ?? ''"
              :min="query.releasedFrom ?? ''"
              @change="onDate('releasedTo', ($event.target as HTMLInputElement).value)"
            />
          </div>
        </div>
      </div>
      <div class="column is-half-mobile is-3-tablet">
        <div class="field">
          <label class="label" for="added-from">Added from</label>
          <div class="control">
            <input
              id="added-from"
              class="input is-small"
              type="date"
              :value="query.addedFrom ?? ''"
              :max="query.addedTo ?? ''"
              @change="onDate('addedFrom', ($event.target as HTMLInputElement).value)"
            />
          </div>
        </div>
      </div>
      <div class="column is-half-mobile is-3-tablet">
        <div class="field">
          <label class="label" for="added-to">Added until</label>
          <div class="control">
            <input
              id="added-to"
              class="input is-small"
              type="date"
              :value="query.addedTo ?? ''"
              :min="query.addedFrom ?? ''"
              @change="onDate('addedTo', ($event.target as HTMLInputElement).value)"
            />
          </div>
        </div>
      </div>
      <div v-if="filtered" class="column is-narrow">
        <div class="field">
          <button type="button" class="button is-small is-light" @click="clearFilter">
            Clear filter
          </button>
        </div>
      </div>
    </div>
  </div>

  <ErrorNotification :error="error" @dismiss="error = null" />

  <template v-if="result">
    <p class="mb-4 has-text-grey">
      {{ result.totalCount }}<template v-if="filtered"> of {{ result.unfilteredCount }}</template>
      {{ result.unfilteredCount === 1 ? 'movie' : 'movies' }} to watch
      <template v-if="filtered"> {{ filterDescription }}</template>
      <span v-if="loading" class="ml-2">· loading…</span>
    </p>

    <template v-if="result.items.length">
      <article v-for="item in result.items" :key="item.movie.id" class="media media-wrap box">
        <figure class="media-left" style="width: 64px">
          <RouterLink :to="`/movies/${item.movie.id}`">
            <Poster :url="item.movie.posterUrl" :title="item.movie.title" />
          </RouterLink>
        </figure>
        <div class="media-content">
          <p>
            <RouterLink class="has-text-weight-semibold" :to="`/movies/${item.movie.id}`">{{
              item.movie.title
            }}</RouterLink>
            <span v-if="item.movie.releaseDate" class="has-text-grey">
              ({{ yearOf(item.movie.releaseDate) }})</span
            >
          </p>
          <p class="is-size-7 has-text-grey">
            Added {{ item.addedAt.slice(0, 10) }}
            <template v-if="item.runtimeMinutes"> · {{ item.runtimeMinutes }} min</template>
          </p>
          <div v-if="item.genres.length" class="tags mt-2 mb-0">
            <RouterLink
              v-for="genre in item.genres"
              :key="genre.id"
              class="tag is-link"
              :class="{ 'is-light': !query.genreIds?.includes(genre.id) }"
              :to="genreFilterLink(genre)"
            >
              {{ genre.name }}
            </RouterLink>
          </div>

          <form v-if="editing === item.movie.id" class="mt-2" @submit.prevent="saveDetails(item)">
            <div class="field">
              <input
                v-model="noteDraft"
                class="input is-small"
                maxlength="500"
                aria-label="Note"
                placeholder="Why do you want to watch it?"
                @keydown.escape="editing = null"
              />
            </div>
            <div class="field">
              <input
                v-model="tagsDraft"
                class="input is-small"
                aria-label="Tags"
                autocomplete="off"
                placeholder="Tags, separated by commas"
                :class="{ 'is-danger': tagsProblem }"
                @keydown.escape="editing = null"
              />
              <p v-if="tagsProblem" class="help is-danger">{{ tagsProblem }}</p>
            </div>
            <div class="buttons are-small">
              <button type="submit" class="button is-primary" :class="{ 'is-loading': saving }">
                Save
              </button>
              <button type="button" class="button" @click="editing = null">Cancel</button>
            </div>
          </form>
          <template v-else>
            <p v-if="item.note" class="mt-2 pre-wrap">{{ item.note }}</p>
            <div class="mt-2">
              <TagList :tags="item.tags" route="/watchlist" :active="query.tag" />
            </div>
          </template>
        </div>
        <div class="media-right">
          <div class="buttons are-small is-right">
            <RouterLink
              class="button is-primary"
              :to="{ path: '/diary/new', query: { movieId: item.movie.id } }"
            >
              Log a viewing
            </RouterLink>
            <button class="button" @click="edit(item)">
              {{ item.note || item.tags.length ? 'Edit note and tags' : 'Add note or tags' }}
            </button>
            <button class="button is-danger is-outlined" @click="remove(item)">Remove</button>
          </div>
        </div>
      </article>
    </template>
    <div v-else-if="filtered" class="notification">
      No movie on your watchlist matches the filter.
      <button type="button" class="button is-small is-ghost" @click="clearFilter">
        Clear filter
      </button>
    </div>
    <div v-else class="notification">Your watchlist is empty. Search a movie above to add it.</div>

    <Pagination
      :page="result.page"
      :page-size="result.pageSize"
      :total-count="result.totalCount"
      @page-change="goToPage"
    />
  </template>
  <progress v-else-if="loading" class="progress is-small is-primary" max="100"></progress>
</template>
