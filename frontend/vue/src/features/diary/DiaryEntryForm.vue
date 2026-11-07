<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useApi } from '../../core/api/api';
import { toApiError, type ApiError } from '../../core/api/api-error';
import type { DiaryEntryRequest, MovieRef } from '../../core/api/models';
import { todayIso } from '../../shared/dates';
import ErrorNotification from '../../shared/ErrorNotification.vue';
import MoviePicker from '../../shared/MoviePicker.vue';
import { RATING_OPTIONS } from '../../shared/rating-format';

const props = defineProps<{
  /** Route parameter; not set when logging a new viewing. */
  id?: string;
  /** Query parameter to preselect the movie, e.g. when coming from a movie page. */
  movieId?: string;
}>();

const router = useRouter();
const api = useApi();

const today = todayIso();
const movie = ref<MovieRef | null>(null);
const movieTouched = ref(false);
const loading = ref(false);
const saving = ref(false);
const error = ref<ApiError | null>(null);

const form = reactive({
  watchedOn: today,
  rating: null as number | null,
  review: '',
  isRewatch: false,
  liked: false,
});

type Field = 'watchedOn' | 'review';

/** What is wrong with the fields. */
const invalid = computed<Record<Field, boolean>>(() => ({
  watchedOn: form.watchedOn === '',
  review: form.review.length > 20000,
}));

/** The fields the user has been in, or has failed to fill in by trying to save. */
const touched = reactive<Record<Field, boolean>>({ watchedOn: false, review: false });

/** Problems are only shown for fields the user has touched, not on a fresh form. */
const isInvalid = (field: Field) => invalid.value[field] && touched[field];

const cancelLink = computed(() =>
  props.movieId && movie.value ? `/movies/${movie.value.id}` : '/diary',
);

watch(
  () => [props.id, props.movieId],
  ([id, movieId]) => {
    if (id) {
      void loadEntry(id);
    } else if (movieId) {
      void preselectMovie(movieId);
    }
  },
  { immediate: true },
);

async function loadEntry(id: string): Promise<void> {
  loading.value = true;
  try {
    const entry = await api.diary.get(id);
    movie.value = entry.movie;
    form.watchedOn = entry.watchedOn;
    form.rating = entry.rating;
    form.review = entry.review ?? '';
    form.isRewatch = entry.isRewatch;
    form.liked = entry.liked;
  } catch (failure) {
    error.value = toApiError(failure);
  }
  loading.value = false;
}

async function preselectMovie(movieId: string): Promise<void> {
  try {
    const preselected = await api.movies.get(movieId);
    movie.value = preselected;
    // Watched before: most likely a rewatch.
    if (preselected.watchCount > 0) {
      form.isRewatch = true;
    }
  } catch (failure) {
    error.value = toApiError(failure);
  }
}

async function save(): Promise<void> {
  const picked = movie.value;
  if (Object.values(invalid.value).some(Boolean) || !picked) {
    touched.watchedOn = true;
    touched.review = true;
    movieTouched.value = true;
    return;
  }

  const request: DiaryEntryRequest = {
    movieId: picked.id,
    watchedOn: form.watchedOn,
    rating: form.rating,
    review: form.review.trim() || null,
    isRewatch: form.isRewatch,
    liked: form.liked,
  };

  saving.value = true;
  error.value = null;
  try {
    const entry = props.id
      ? await api.diary.update(props.id, request)
      : await api.diary.create(request);
    void router.push(`/movies/${entry.movie.id}`);
  } catch (failure) {
    saving.value = false;
    error.value = toApiError(failure);
  }
}
</script>

<template>
  <h1 class="title">{{ id ? 'Edit diary entry' : 'Log a viewing' }}</h1>

  <ErrorNotification :error="error" @dismiss="error = null" />

  <progress v-if="loading" class="progress is-small is-primary" max="100"></progress>
  <form v-else class="box" novalidate @submit.prevent="save">
    <div class="field">
      <label class="label" for="movie">Movie *</label>
      <MoviePicker
        v-model:selected="movie"
        input-id="movie"
        after-manual-add="diary"
        :invalid="movieTouched && !movie"
      />
      <p v-if="movieTouched && !movie" class="help is-danger">Please pick a movie.</p>
    </div>

    <div class="columns">
      <div class="column">
        <div class="field">
          <label class="label" for="watchedOn">Watched on *</label>
          <div class="control">
            <input
              id="watchedOn"
              v-model="form.watchedOn"
              class="input"
              type="date"
              :max="today"
              :class="{ 'is-danger': isInvalid('watchedOn') }"
              @input="touched.watchedOn = true"
              @blur="touched.watchedOn = true"
            />
          </div>
          <p v-if="isInvalid('watchedOn')" class="help is-danger">
            Please enter the date you watched the movie.
          </p>
        </div>
      </div>
      <div class="column">
        <div class="field">
          <label class="label" for="rating">Rating</label>
          <div class="control">
            <div class="select is-fullwidth">
              <select id="rating" v-model="form.rating">
                <option :value="null">No rating</option>
                <option v-for="option in RATING_OPTIONS" :key="option.value" :value="option.value">
                  {{ option.label }}
                </option>
              </select>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="field">
      <label class="label" for="review">Review</label>
      <div class="control">
        <textarea
          id="review"
          v-model="form.review"
          class="textarea"
          rows="5"
          :class="{ 'is-danger': isInvalid('review') }"
          @input="touched.review = true"
          @blur="touched.review = true"
        ></textarea>
      </div>
      <p v-if="isInvalid('review')" class="help is-danger">At most 20,000 characters.</p>
    </div>

    <div class="field is-grouped is-grouped-multiline">
      <div class="control">
        <label class="checkbox"><input v-model="form.isRewatch" type="checkbox" /> Rewatch</label>
      </div>
      <div class="control">
        <label class="checkbox"><input v-model="form.liked" type="checkbox" /> Liked ♥</label>
      </div>
    </div>

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
