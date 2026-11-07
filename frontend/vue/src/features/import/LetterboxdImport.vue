<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useApi } from '../../core/api/api';
import { toApiError, type ApiError } from '../../core/api/api-error';
import { HttpError } from '../../core/api/http';
import type { ImportFilm, ImportJob } from '../../core/api/models';
import ErrorNotification from '../../shared/ErrorNotification.vue';

const POLL_INTERVAL_MS = 1000;

/**
 * Uploads a Letterboxd export. The server imports it in the background, which takes a while for a big
 * diary (every film is looked up on TMDB), so this page shows the progress. The import's ID is in the URL
 * (?job=...): reloading the page, or coming back later, shows the progress or result again.
 */
const props = defineProps<{
  /** The ID of the import in the URL. */
  jobId?: string;
}>();

const router = useRouter();
const api = useApi();

const file = ref<File | null>(null);
const uploading = ref(false);
const job = ref<ImportJob | null>(null);
const error = ref<ApiError | null>(null);
/** The server does not know the import in the URL (any more), e.g. because it was restarted. */
const lost = ref(false);

let stopPolling: (() => void) | undefined;

watch(
  () => props.jobId,
  (id) => {
    stopPolling?.();
    stopPolling = undefined;
    lost.value = false;
    if (!id) {
      job.value = null;
      return;
    }
    stopPolling = poll(id);
  },
  { immediate: true },
);

onBeforeUnmount(() => stopPolling?.());

/** Asks for the job until it has finished, one request at a time. Returns a function that stops it. */
function poll(id: string): () => void {
  let stopped = false;
  let timer: ReturnType<typeof setTimeout> | undefined;

  async function ask(): Promise<void> {
    let current: ImportJob;
    try {
      current = await api.letterboxdImport.get(id);
    } catch (failure) {
      if (stopped) {
        return;
      }
      if (failure instanceof HttpError && failure.status === 404) {
        lost.value = true;
      } else {
        error.value = toApiError(failure);
      }
      return;
    }
    if (stopped) {
      return;
    }
    job.value = current;
    if (current.status === 'running') {
      timer = setTimeout(ask, POLL_INTERVAL_MS);
    }
  }

  void ask();
  return () => {
    stopped = true;
    clearTimeout(timer);
  };
}

function chooseFile(event: Event): void {
  file.value = (event.target as HTMLInputElement).files?.[0] ?? null;
  error.value = null;
}

async function start(): Promise<void> {
  const chosen = file.value;
  if (!chosen || uploading.value) {
    return;
  }

  uploading.value = true;
  error.value = null;
  try {
    const started = await api.letterboxdImport.start(chosen);
    uploading.value = false;
    job.value = started;
    void router.push({ query: { job: started.id } });
  } catch (failure) {
    uploading.value = false;
    error.value = toApiError(failure);
  }
}

/** Back to the upload form. */
function reset(): void {
  file.value = null;
  error.value = null;
  void router.push({ query: {} });
}

function filmLabel(film: ImportFilm): string {
  return film.year ? `${film.name} (${film.year})` : film.name;
}
</script>

<template>
  <h1 class="title">Import from Letterboxd</h1>

  <ErrorNotification :error="error" @dismiss="error = null" />

  <template v-if="job">
    <div v-if="job.status === 'running'" class="box">
      <p v-if="job.filmsTotal" class="mb-3">
        Looking up your films on TMDB: {{ job.filmsProcessed }} of {{ job.filmsTotal }}
      </p>
      <p v-else class="mb-3">Starting the import…</p>
      <progress
        class="progress is-primary"
        :value.attr="job.filmsTotal ? job.filmsProcessed : undefined"
        :max.attr="job.filmsTotal || undefined"
      ></progress>
      <p class="help">
        A big diary takes a minute or two. You can leave this page and come back to it later.
      </p>
    </div>

    <template v-else-if="job.status === 'completed'">
      <template v-if="job.result">
        <div class="notification is-success is-light">
          <p class="has-text-weight-semibold mb-2">The import is finished.</p>
          <ul>
            <li>
              Diary entries imported: {{ job.result.diaryEntriesImported }}
              <span v-if="job.result.diaryEntriesSkipped" class="has-text-grey">
                ({{ job.result.diaryEntriesSkipped }} skipped, they were in your diary already)
              </span>
            </li>
            <li>
              Watchlist items imported: {{ job.result.watchlistItemsImported }}
              <span v-if="job.result.watchlistItemsSkipped" class="has-text-grey">
                ({{ job.result.watchlistItemsSkipped }} skipped, they were on your watchlist
                already)
              </span>
            </li>
          </ul>
          <div class="buttons mt-3">
            <RouterLink class="button is-small" to="/diary">Go to the diary</RouterLink>
            <RouterLink class="button is-small" to="/watchlist">Go to the watchlist</RouterLink>
          </div>
        </div>

        <div v-if="job.result.failedFilms.length" class="notification is-danger is-light">
          <p class="mb-2">
            TMDB did not answer for {{ job.result.failedFilms.length }} films, so they were not
            imported. Upload the same file again later to add them; everything else is skipped then.
          </p>
          <ul>
            <li v-for="(film, index) in job.result.failedFilms" :key="index">
              {{ filmLabel(film) }}
            </li>
          </ul>
        </div>

        <div v-if="job.result.manualMovies.length" class="notification is-warning is-light">
          <details>
            <summary>
              {{ job.result.manualMovies.length }} films could not be found on TMDB and were added
              as manual movies
            </summary>
            <p class="my-2">
              Manual movies are private to you and have no poster or genres. Find them under
              <RouterLink to="/movies">My movies</RouterLink> to complete them.
            </p>
            <ul>
              <li v-for="(film, index) in job.result.manualMovies" :key="index">
                {{ filmLabel(film) }}
              </li>
            </ul>
          </details>
        </div>

        <div v-if="job.result.warnings.length" class="notification is-light">
          <details>
            <summary>Some rows of the export were skipped</summary>
            <ul class="mt-2">
              <li v-for="(warning, index) in job.result.warnings" :key="index">{{ warning }}</li>
            </ul>
          </details>
        </div>
      </template>
      <button class="button" @click="reset">Import another file</button>
    </template>

    <template v-else-if="job.status === 'failed'">
      <div class="notification is-danger is-light" role="alert">{{ job.error }}</div>
      <button class="button" @click="reset">Try again</button>
    </template>
  </template>

  <template v-else>
    <div v-if="lost" class="notification is-warning is-light" role="alert">
      This import is not available any more, for example because the server was restarted. Upload
      the file again: what was imported already is skipped.
    </div>

    <div class="content">
      <p>
        Brings your diary and watchlist over from
        <a href="https://letterboxd.com/" target="_blank" rel="noopener">Letterboxd</a>.
      </p>
      <ol>
        <li>
          On letterboxd.com, open <strong>Settings</strong>, then <strong>Data</strong>, and choose
          <strong>Export your data</strong>.
        </li>
        <li>Upload the ZIP archive you get, without unpacking it.</li>
      </ol>
      <p class="is-size-7 has-text-grey">
        Imported are the diary (dates, ratings, rewatches, reviews, likes) and the watchlist. The
        export has no TMDB IDs, so every film is looked up on TMDB by title and year; films that
        TMDB does not know become manual movies. The other files of the archive, such as your
        profile, are neither read nor stored. Importing the same export again is safe: what exists
        already is skipped.
      </p>
    </div>

    <form class="box" @submit.prevent="start">
      <div class="field">
        <div class="file has-name is-fullwidth">
          <label class="file-label">
            <input
              class="file-input"
              type="file"
              name="export"
              accept=".zip,application/zip"
              @change="chooseFile"
            />
            <span class="file-cta">
              <span class="file-label">Choose the ZIP archive…</span>
            </span>
            <span class="file-name">{{ file?.name ?? 'No file chosen' }}</span>
          </label>
        </div>
      </div>
      <button
        type="submit"
        class="button is-primary"
        :class="{ 'is-loading': uploading }"
        :disabled="!file || uploading"
      >
        Start the import
      </button>
    </form>
  </template>
</template>
