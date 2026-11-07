<script setup lang="ts">
import type { ApiError } from '../core/api/api-error';

defineProps<{
  error?: ApiError | null;
}>();

defineEmits<{
  dismiss: [];
}>();
</script>

<template>
  <div v-if="error" class="notification is-danger is-light" role="alert">
    <button class="delete" aria-label="Dismiss" @click="$emit('dismiss')"></button>
    <p>{{ error.message }}</p>
    <ul v-if="error.details.length" class="mt-2">
      <li v-for="(detail, index) in error.details" :key="index">&bull; {{ detail }}</li>
    </ul>
  </div>
</template>
