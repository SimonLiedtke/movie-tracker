<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { useRouter, type RouteLocationRaw } from 'vue-router';
import { useApi } from '../../core/api/api';
import { toApiError, type ApiError } from '../../core/api/api-error';
import type { Genre, Movie, MovieRequest } from '../../core/api/models';
import ErrorNotification from '../../shared/ErrorNotification.vue';

/** Empty strings become null, the backend expects absent values as null. */
const orNull = (value: string): string | null => (value === '' ? null : value);

const POSTER_URL_PATTERN = /^https?:\/\/\S+$/;

/**
 * Adds or edits a manually added movie: one that TMDB does not know, or that the user wants to describe
 * themselves. Only its owner sees it. Movies from TMDB cannot be edited.
 */
const props = defineProps<{
  /** Route parameter; not set when adding a movie. */
  id?: string;
  /** Query parameter: title to start with, e.g. what the user searched for. */
  title?: string;
  /** Query parameter: where to continue after adding, e.g. back to logging a viewing ('diary' or 'watchlist'). */
  next?: string;
}>();

const router = useRouter();
const api = useApi();

const genres = ref<Genre[]>([]);
api.genres.list().then(
  (loaded) => (genres.value = loaded),
  () => undefined, // Without genres, the form just has no choices.
);

const loading = ref(false);
const saving = ref(false);
const error = ref<ApiError | null>(null);

const form = reactive({
  title: '',
  originalTitle: '',
  releaseDate: '',
  runtimeMinutes: null as number | null,
  overview: '',
  posterUrl: '',
  genreIds: [] as number[],
});

type Field = 'title' | 'originalTitle' | 'runtimeMinutes' | 'overview' | 'posterUrl';

/** What is wrong with the fields. */
const invalid = computed<Record<Field, boolean>>(() => ({
  title: form.title.length === 0 || form.title.length > 300,
  originalTitle: form.originalTitle.length > 300,
  runtimeMinutes:
    form.runtimeMinutes !== null && (form.runtimeMinutes < 1 || form.runtimeMinutes > 1500),
  overview: form.overview.length > 5000,
  posterUrl:
    form.posterUrl.length > 0 &&
    (form.posterUrl.length > 2000 || !POSTER_URL_PATTERN.test(form.posterUrl)),
}));

/** The fields the user has been in, or has failed to fill in by trying to save. */
const touched = reactive<Record<Field, boolean>>({
  title: false,
  originalTitle: false,
  runtimeMinutes: false,
  overview: false,
  posterUrl: false,
});

/** Problems are only shown for fields the user has touched, not on a fresh form. */
const isInvalid = (field: Field) => invalid.value[field] && touched[field];

const cancelLink = computed(() => {
  if (props.id) {
    return `/movies/${props.id}`;
  }
  return props.next === 'diary'
    ? '/diary/new'
    : props.next === 'watchlist'
      ? '/watchlist'
      : '/movies';
});

/** Numbers the loads, so that the answer to an outdated one is ignored. */
let latestLoad = 0;

watch(
  () => [props.id, props.title],
  ([id, title]) => {
    if (id) {
      void load(id);
    } else if (title) {
      form.title = title;
    }
  },
  { immediate: true },
);

async function load(id: string): Promise<void> {
  const thisLoad = ++latestLoad;
  loading.value = true;
  try {
    const movie = await api.movies.get(id);
    if (thisLoad === latestLoad) {
      fill(movie);
    }
  } catch (failure) {
    if (thisLoad === latestLoad) {
      error.value = toApiError(failure);
    }
  }
  if (thisLoad === latestLoad) {
    loading.value = false;
  }
}

function fill(movie: Movie): void {
  form.title = movie.title;
  form.originalTitle = movie.originalTitle ?? '';
  form.releaseDate = movie.releaseDate ?? '';
  form.runtimeMinutes = movie.runtimeMinutes;
  form.overview = movie.overview ?? '';
  form.posterUrl = movie.posterUrl ?? '';
  form.genreIds = movie.genres.map((genre) => genre.id);
}

function onRuntime(value: string): void {
  form.runtimeMinutes = value === '' ? null : Number(value);
  touched.runtimeMinutes = true;
}

async function save(): Promise<void> {
  if (Object.values(invalid.value).some(Boolean)) {
    for (const field of Object.keys(touched) as Field[]) {
      touched[field] = true;
    }
    return;
  }

  const request: MovieRequest = {
    title: form.title.trim(),
    originalTitle: orNull(form.originalTitle.trim()),
    releaseDate: orNull(form.releaseDate),
    runtimeMinutes: form.runtimeMinutes,
    overview: orNull(form.overview.trim()),
    posterUrl: orNull(form.posterUrl.trim()),
    genreIds: [...form.genreIds],
  };

  saving.value = true;
  error.value = null;
  let next: RouteLocationRaw;
  try {
    next = props.id ? await update(props.id, request) : await addAndContinue(request);
  } catch (failure) {
    saving.value = false;
    error.value = toApiError(failure);
    return;
  }
  void router.push(next);
}

async function update(id: string, request: MovieRequest): Promise<RouteLocationRaw> {
  const movie = await api.movies.update(id, request);
  return `/movies/${movie.id}`;
}

/** Creates the movie, then continues where the user came from. */
async function addAndContinue(request: MovieRequest): Promise<RouteLocationRaw> {
  const movie = await api.movies.create(request);
  switch (props.next) {
    case 'diary':
      return { path: '/diary/new', query: { movieId: movie.id } };
    case 'watchlist':
      await api.watchlist.put(movie.id, null);
      return '/watchlist';
    default:
      return `/movies/${movie.id}`;
  }
}
</script>

<template>
  <h1 class="title">{{ id ? 'Edit movie' : 'Add a movie manually' }}</h1>
  <p v-if="!id" class="subtitle is-6 has-text-grey">
    For movies that TMDB does not know. Only you can see the movies you add here.
  </p>

  <ErrorNotification :error="error" @dismiss="error = null" />

  <progress v-if="loading" class="progress is-small is-primary" max="100"></progress>
  <form v-else class="box" novalidate @submit.prevent="save">
    <div class="columns">
      <div class="column">
        <div class="field">
          <label class="label" for="title">Title *</label>
          <div class="control">
            <input
              id="title"
              v-model="form.title"
              class="input"
              :class="{ 'is-danger': isInvalid('title') }"
              @input="touched.title = true"
              @blur="touched.title = true"
            />
          </div>
          <p v-if="isInvalid('title')" class="help is-danger">
            Please enter a title (at most 300 characters).
          </p>
        </div>
      </div>
      <div class="column">
        <div class="field">
          <label class="label" for="originalTitle">Original title</label>
          <div class="control">
            <input
              id="originalTitle"
              v-model="form.originalTitle"
              class="input"
              :class="{ 'is-danger': isInvalid('originalTitle') }"
              @input="touched.originalTitle = true"
              @blur="touched.originalTitle = true"
            />
          </div>
          <p v-if="isInvalid('originalTitle')" class="help is-danger">At most 300 characters.</p>
        </div>
      </div>
    </div>

    <div class="columns">
      <div class="column">
        <div class="field">
          <label class="label" for="releaseDate">Release date</label>
          <div class="control">
            <input id="releaseDate" v-model="form.releaseDate" class="input" type="date" />
          </div>
        </div>
      </div>
      <div class="column">
        <div class="field">
          <label class="label" for="runtimeMinutes">Runtime (minutes)</label>
          <div class="control">
            <input
              id="runtimeMinutes"
              class="input"
              type="number"
              min="1"
              max="1500"
              :value="form.runtimeMinutes ?? ''"
              :class="{ 'is-danger': isInvalid('runtimeMinutes') }"
              @input="onRuntime(($event.target as HTMLInputElement).value)"
              @blur="touched.runtimeMinutes = true"
            />
          </div>
          <p v-if="isInvalid('runtimeMinutes')" class="help is-danger">
            Between 1 and 1500 minutes.
          </p>
        </div>
      </div>
    </div>

    <div class="field">
      <label class="label" for="posterUrl">Poster URL</label>
      <div class="control">
        <input
          id="posterUrl"
          v-model="form.posterUrl"
          class="input"
          type="url"
          placeholder="https://…"
          :class="{ 'is-danger': isInvalid('posterUrl') }"
          @input="touched.posterUrl = true"
          @blur="touched.posterUrl = true"
        />
      </div>
      <p v-if="isInvalid('posterUrl')" class="help is-danger">Please enter a valid http(s) URL.</p>
    </div>

    <div class="field">
      <label class="label" for="overview">Overview</label>
      <div class="control">
        <textarea
          id="overview"
          v-model="form.overview"
          class="textarea"
          rows="4"
          :class="{ 'is-danger': isInvalid('overview') }"
          @input="touched.overview = true"
          @blur="touched.overview = true"
        ></textarea>
      </div>
      <p v-if="isInvalid('overview')" class="help is-danger">At most 5000 characters.</p>
    </div>

    <fieldset class="field">
      <legend class="label">Genres</legend>
      <div class="columns is-multiline is-mobile is-gapless">
        <div
          v-for="genre in genres"
          :key="genre.id"
          class="column is-half-mobile is-one-quarter-tablet is-2-desktop"
        >
          <label class="checkbox">
            <input v-model="form.genreIds" type="checkbox" :value="genre.id" />
            {{ genre.name }}
          </label>
        </div>
      </div>
    </fieldset>

    <div class="field is-grouped mt-5">
      <div class="control">
        <button type="submit" class="button is-primary" :class="{ 'is-loading': saving }">
          Save
        </button>
      </div>
      <div class="control">
        <RouterLink class="button is-light" :to="cancelLink">Cancel</RouterLink>
      </div>
    </div>
  </form>
</template>
