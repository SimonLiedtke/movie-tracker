<script setup lang="ts">
import { ref, watch } from 'vue';

const props = withDefaults(
  defineProps<{
    url?: string | null;
    title?: string;
  }>(),
  { title: '' },
);

const failed = ref(false);

// A different image gets its chance.
watch(
  () => props.url,
  () => (failed.value = false),
);
</script>

<template>
  <figure class="image is-2by3">
    <img
      v-if="url && !failed"
      :src="url"
      :alt="'Poster of ' + title"
      loading="lazy"
      @error="failed = true"
    />
    <div v-else class="has-ratio poster-placeholder" aria-hidden="true">🎬</div>
  </figure>
</template>
