<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useApi } from '../../core/api/api';
import { toApiError, type ApiError } from '../../core/api/api-error';
import type {
  Genre,
  MovieQuery,
  MovieRef,
  MovieSort,
  MovieSummary,
  PagedResponse,
} from '../../core/api/models';
import { yearOf } from '../../shared/dates';
import ErrorNotification from '../../shared/ErrorNotification.vue';
import MoviePicker from '../../shared/MoviePicker.vue';
import Pagination from '../../shared/Pagination.vue';
import Poster from '../../shared/Poster.vue';
import { mergeQuery } from '../../shared/query';
import Rating from '../../shared/Rating.vue';

const PAGE_SIZE = 24;
const SEARCH_DEBOUNCE_MS = 300;

const SORT_OPTIONS: { value: MovieSort; label: string }[] = [
  { value: 'title', label: 'Title (A–Z)' },
  { value: '-title', label: 'Title (Z–A)' },
  { value: '-releaseDate', label: 'Newest releases' },
  { value: 'releaseDate', label: 'Oldest releases' },
  { value: '-createdAt', label: 'Recently added' },
  { value: 'createdAt', label: 'First added' },
];

/** The filters, from the route's query parameters. */
const props = defineProps<{
  search?: string;
  genreId?: string;
  year?: string;
  sort?: string;
  page?: string;
}>();

const router = useRouter();
const api = useApi();

const number = (value: string | undefined) =>
  value === undefined ? undefined : Number(value) || undefined;

const query = computed<MovieQuery>(() => ({
  search: props.search,
  genreId: number(props.genreId),
  year: number(props.year),
  sort: SORT_OPTIONS.find((option) => option.value === props.sort)?.value ?? 'title',
  page: number(props.page) ?? 1,
  pageSize: PAGE_SIZE,
}));

const genres = ref<Genre[]>([]);
api.genres.list().then(
  (loaded) => (genres.value = loaded),
  () => undefined, // Without genres, the filter just has no choices.
);

const result = ref<PagedResponse<MovieSummary> | null>(null);
const loading = ref(true);
const error = ref<ApiError | null>(null);

/** Numbers the requests, so that the answer to an outdated one is ignored. */
let latestLoad = 0;

async function load(movieQuery: MovieQuery): Promise<void> {
  const thisLoad = ++latestLoad;
  loading.value = true;
  try {
    const loaded = await api.movies.list(movieQuery);
    if (thisLoad !== latestLoad) {
      return;
    }
    error.value = null;
    result.value = loaded;
  } catch (failure) {
    if (thisLoad !== latestLoad) {
      return;
    }
    error.value = toApiError(failure);
    result.value = null;
  }
  loading.value = false;
}

watch(query, load, { immediate: true });

// The fields show the URL's values, but keep what the user types until the URL has caught up.
const searchText = ref(query.value.search ?? '');
const yearText = ref(String(query.value.year ?? ''));
watch(
  () => query.value.search,
  (search) => (searchText.value = search ?? ''),
);
watch(
  () => query.value.year,
  (year) => (yearText.value = String(year ?? '')),
);

let searchTimer: ReturnType<typeof setTimeout> | undefined;

function onSearch(value: string): void {
  searchText.value = value;
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    // Not trimmed here: the input shows the URL value, so trimming would eat spaces while typing.
    if (value !== (props.search ?? '')) {
      updateQuery({ search: value || undefined }, true);
    }
  }, SEARCH_DEBOUNCE_MS);
}

function onFilter(key: 'genreId' | 'year' | 'sort', value: string): void {
  updateQuery({ [key]: value || undefined });
}

function openMovie(movie: MovieRef): void {
  router.push(`/movies/${movie.id}`);
}

function goToPage(page: number): void {
  router.push({ query: mergeQuery(router.currentRoute.value.query, { page }) });
}

function clearFilters(): void {
  router.push({ query: {} });
}

/** Changing a filter starts again at page 1. */
function updateQuery(changes: Record<string, string | undefined>, replace = false): void {
  const merged = mergeQuery(router.currentRoute.value.query, { ...changes, page: undefined });
  if (replace) {
    router.replace({ query: merged });
  } else {
    router.push({ query: merged });
  }
}
</script>

<template>
  <h1 class="title">My movies</h1>
  <p class="subtitle is-6 has-text-grey">
    The movies you have logged, put on your watchlist or added manually.
  </p>

  <div class="field">
    <label class="label" for="find">Find any movie</label>
    <MoviePicker
      input-id="find"
      placeholder="Search TMDB and your own movies"
      :keep-selection="false"
      @picked="openMovie"
    />
  </div>

  <div class="box">
    <div class="columns is-multiline">
      <div class="column is-12-tablet is-5-desktop">
        <div class="field">
          <label class="label" for="search">Search my movies</label>
          <div class="control">
            <input
              id="search"
              class="input"
              type="search"
              placeholder="Title or original title"
              :value="searchText"
              @input="onSearch(($event.target as HTMLInputElement).value)"
            />
          </div>
        </div>
      </div>
      <div class="column is-4-tablet is-2-desktop">
        <div class="field">
          <label class="label" for="genre">Genre</label>
          <div class="control">
            <div class="select is-fullwidth">
              <select
                id="genre"
                @change="onFilter('genreId', ($event.target as HTMLSelectElement).value)"
              >
                <option value="" :selected="!query.genreId">All genres</option>
                <option
                  v-for="genre in genres"
                  :key="genre.id"
                  :value="genre.id"
                  :selected="genre.id === query.genreId"
                >
                  {{ genre.name }}
                </option>
              </select>
            </div>
          </div>
        </div>
      </div>
      <div class="column is-4-tablet is-2-desktop">
        <div class="field">
          <label class="label" for="year">Release year</label>
          <div class="control">
            <input
              id="year"
              class="input"
              type="number"
              min="1870"
              max="2100"
              placeholder="Any"
              :value="yearText"
              @input="yearText = ($event.target as HTMLInputElement).value"
              @change="onFilter('year', ($event.target as HTMLInputElement).value)"
            />
          </div>
        </div>
      </div>
      <div class="column is-4-tablet is-3-desktop">
        <div class="field">
          <label class="label" for="sort">Sort by</label>
          <div class="control">
            <div class="select is-fullwidth">
              <select
                id="sort"
                @change="onFilter('sort', ($event.target as HTMLSelectElement).value)"
              >
                <option
                  v-for="option in SORT_OPTIONS"
                  :key="option.value"
                  :value="option.value"
                  :selected="option.value === query.sort"
                >
                  {{ option.label }}
                </option>
              </select>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>

  <ErrorNotification :error="error" @dismiss="error = null" />

  <template v-if="result">
    <p class="mb-4 has-text-grey">
      {{ result.totalCount }} {{ result.totalCount === 1 ? 'movie' : 'movies' }}
      <span v-if="loading" class="ml-2">· loading…</span>
    </p>

    <div v-if="result.items.length" class="columns is-multiline is-mobile">
      <div
        v-for="movie in result.items"
        :key="movie.id"
        class="column is-half-mobile is-one-third-tablet is-one-fifth-desktop is-2-widescreen"
      >
        <RouterLink class="card is-block" :to="`/movies/${movie.id}`">
          <div class="card-image">
            <Poster :url="movie.posterUrl" :title="movie.title" />
          </div>
          <div class="card-content p-3">
            <p class="has-text-weight-semibold">{{ movie.title }}</p>
            <p class="is-size-7 has-text-grey">
              {{ yearOf(movie.releaseDate) }}
              <template v-if="movie.watchCount">· watched {{ movie.watchCount }}×</template>
            </p>
            <div class="is-flex is-justify-content-space-between is-align-items-center mt-1">
              <Rating :half-stars="movie.latestRating" />
              <span v-if="movie.onWatchlist" class="tag is-info is-light" title="On your watchlist"
                >Watchlist</span
              >
            </div>
          </div>
        </RouterLink>
      </div>
    </div>
    <div v-else class="notification">
      No movies found.
      <button
        v-if="query.search || query.genreId || query.year"
        class="button is-small is-ghost"
        @click="clearFilters"
      >
        Clear filters
      </button>
      <template v-else
        >Find a movie above, then log a viewing or put it on your watchlist.</template
      >
    </div>

    <Pagination
      :page="result.page"
      :page-size="result.pageSize"
      :total-count="result.totalCount"
      @page-change="goToPage"
    />
  </template>
  <progress v-else-if="loading" class="progress is-small is-primary" max="100"></progress>
</template>
