<script setup lang="ts">
import { ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useApi } from '../../core/api/api';
import { toApiError, type ApiError } from '../../core/api/api-error';
import type { DiaryEntry, Movie } from '../../core/api/models';
import { yearOf } from '../../shared/dates';
import ErrorNotification from '../../shared/ErrorNotification.vue';
import Poster from '../../shared/Poster.vue';
import Rating from '../../shared/Rating.vue';

const props = defineProps<{
  /** Route parameter. */
  id: string;
}>();

const router = useRouter();
const api = useApi();

const movie = ref<Movie | null>(null);
const entries = ref<DiaryEntry[]>([]);
const loading = ref(true);
const notFound = ref(false);
const busy = ref(false);
const error = ref<ApiError | null>(null);

/** Numbers the requests, so that the answer to an outdated one is ignored. */
let latestLoad = 0;

async function load(id: string): Promise<void> {
  const thisLoad = ++latestLoad;
  loading.value = true;
  try {
    const [loadedMovie, loadedEntries] = await Promise.all([
      api.movies.get(id),
      api.movies.diaryEntries(id),
    ]);
    if (thisLoad !== latestLoad) {
      return;
    }
    movie.value = loadedMovie;
    entries.value = loadedEntries;
    notFound.value = false;
  } catch (failure) {
    if (thisLoad !== latestLoad) {
      return;
    }
    const apiError = toApiError(failure);
    notFound.value = apiError.status === 404;
    error.value = apiError.status === 404 ? null : apiError;
  }
  loading.value = false;
}

watch(() => props.id, load, { immediate: true });

function toggleWatchlist(current: Movie): void {
  const request = current.onWatchlist
    ? api.watchlist.remove(current.id)
    : api.watchlist.put(current.id, null);
  void run(request, () => (movie.value = { ...current, onWatchlist: !current.onWatchlist }));
}

function deleteMovie(current: Movie): void {
  if (!confirm(`Delete "${current.title}"? This cannot be undone.`)) {
    return;
  }
  void run(api.movies.delete(current.id), () => router.push('/movies'));
}

function deleteEntry(entry: DiaryEntry): void {
  if (!confirm(`Delete the diary entry from ${entry.watchedOn}?`)) {
    return;
  }
  // Reload so that the movie's watch count and latest rating are up to date.
  void run(api.diary.delete(entry.id), () => load(props.id));
}

async function run(request: Promise<unknown>, onSuccess: () => unknown): Promise<void> {
  busy.value = true;
  error.value = null;
  try {
    await request;
  } catch (failure) {
    busy.value = false;
    error.value = toApiError(failure);
    return;
  }
  busy.value = false;
  onSuccess();
}
</script>

<template>
  <ErrorNotification :error="error" @dismiss="error = null" />

  <template v-if="movie">
    <div class="columns">
      <div class="column is-one-quarter-tablet is-one-fifth-desktop">
        <Poster :url="movie.posterUrl" :title="movie.title" />
      </div>

      <div class="column">
        <h1 class="title">
          {{ movie.title }}
          <span v-if="movie.releaseDate" class="has-text-grey has-text-weight-normal"
            >({{ yearOf(movie.releaseDate) }})</span
          >
        </h1>
        <p
          v-if="movie.originalTitle && movie.originalTitle !== movie.title"
          class="subtitle is-6 has-text-grey"
        >
          Original title: {{ movie.originalTitle }}
        </p>

        <div class="tags">
          <RouterLink
            v-for="genre in movie.genres"
            :key="genre.id"
            class="tag is-link is-light"
            :to="{ path: '/movies', query: { genreId: genre.id } }"
          >
            {{ genre.name }}
          </RouterLink>
        </div>

        <p v-if="movie.overview" class="block pre-wrap">{{ movie.overview }}</p>

        <div class="field is-grouped is-grouped-multiline block">
          <div v-if="movie.releaseDate" class="control">
            <div class="tags has-addons">
              <span class="tag is-dark">Released</span
              ><span class="tag">{{ movie.releaseDate }}</span>
            </div>
          </div>
          <div v-if="movie.runtimeMinutes" class="control">
            <div class="tags has-addons">
              <span class="tag is-dark">Runtime</span
              ><span class="tag">{{ movie.runtimeMinutes }} min</span>
            </div>
          </div>
          <div class="control">
            <div class="tags has-addons">
              <span class="tag is-dark">Watched</span
              ><span class="tag">{{ movie.watchCount }}×</span>
            </div>
          </div>
          <div v-if="movie.lastWatchedOn" class="control">
            <div class="tags has-addons">
              <span class="tag is-dark">Last watched</span
              ><span class="tag">{{ movie.lastWatchedOn }}</span>
            </div>
          </div>
          <div v-if="movie.tmdbId" class="control">
            <a
              class="tags has-addons"
              :href="'https://www.themoviedb.org/movie/' + movie.tmdbId"
              target="_blank"
              rel="noopener"
              title="Details from TMDB"
            >
              <span class="tag is-dark">TMDB</span><span class="tag">{{ movie.tmdbId }}</span>
            </a>
          </div>
          <div v-else class="control">
            <span class="tag is-warning is-light" title="Only you can see this movie"
              >Added manually</span
            >
          </div>
        </div>

        <p v-if="movie.latestRating" class="block is-size-4">
          <Rating :half-stars="movie.latestRating" />
        </p>

        <div class="buttons">
          <RouterLink
            class="button is-primary"
            :to="{ path: '/diary/new', query: { movieId: movie.id } }"
            >Log a viewing</RouterLink
          >
          <button
            class="button"
            :class="{ 'is-info': !movie.onWatchlist, 'is-light': movie.onWatchlist }"
            :disabled="busy"
            @click="toggleWatchlist(movie)"
          >
            {{ movie.onWatchlist ? 'Remove from watchlist' : 'Add to watchlist' }}
          </button>
          <template v-if="movie.tmdbId === null">
            <RouterLink class="button" :to="`/movies/${movie.id}/edit`">Edit</RouterLink>
            <button
              class="button is-danger is-outlined"
              :disabled="busy"
              @click="deleteMovie(movie)"
            >
              Delete
            </button>
          </template>
        </div>
      </div>
    </div>

    <h2 class="title is-4 mt-5">Viewings</h2>
    <div v-if="entries.length" class="table-container">
      <table class="table is-fullwidth is-hoverable">
        <thead>
          <tr>
            <th>Watched on</th>
            <th>Rating</th>
            <th>Review</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="entry in entries" :key="entry.id">
            <td class="is-nowrap">
              {{ entry.watchedOn }}
              <span v-if="entry.isRewatch" class="tag is-light ml-1" title="Rewatch">↻</span>
              <span v-if="entry.liked" class="has-text-danger ml-1" title="Liked">♥</span>
            </td>
            <td><Rating :half-stars="entry.rating" /></td>
            <td class="pre-wrap">{{ entry.review }}</td>
            <td class="has-text-right">
              <div class="buttons is-right are-small">
                <RouterLink class="button" :to="`/diary/${entry.id}/edit`">Edit</RouterLink>
                <button
                  class="button is-danger is-outlined"
                  :disabled="busy"
                  @click="deleteEntry(entry)"
                >
                  Delete
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <p v-else class="has-text-grey">Not watched yet.</p>
  </template>
  <div v-else-if="notFound" class="notification is-warning is-light">
    This movie does not exist (anymore). <RouterLink to="/movies">Back to your movies</RouterLink>
  </div>
  <progress v-else-if="loading" class="progress is-small is-primary" max="100"></progress>
</template>
