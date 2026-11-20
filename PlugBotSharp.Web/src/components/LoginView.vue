<script setup>
import { reactive, ref } from 'vue'
import { login } from '../auth'

const emit = defineEmits(['done'])

const form = reactive({ user: '', password: '' })
const busy = ref(false)
const error = ref('')

async function submit() {
  error.value = ''

  if (!form.user.trim() || !form.password) {
    error.value = '请输入用户名和密码'
    return
  }

  busy.value = true

  try {
    await login(form.user.trim(), form.password)
    emit('done')
  } catch (e) {
    error.value = e.message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="auth-page">
    <form class="auth-card" @submit.prevent="submit">
      <div class="auth-brand">PlugBotSharp</div>
      <p class="auth-sub">管理面板登录</p>

      <div class="field">
        <label>用户名</label>
        <input v-model="form.user" autocomplete="username" autofocus />
      </div>

      <div class="field">
        <label>密码</label>
        <input v-model="form.password" type="password" autocomplete="current-password" />
      </div>

      <p v-if="error" class="error">{{ error }}</p>

      <button class="btn primary block" type="submit" :disabled="busy">
        {{ busy ? '登录中…' : '登录' }}
      </button>

      <p class="auth-tip">连续输错会被临时锁定</p>
    </form>
  </div>
</template>
