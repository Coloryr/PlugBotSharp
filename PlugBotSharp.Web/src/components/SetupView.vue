<script setup>
import { reactive, ref } from 'vue'
import { initAccount } from '../auth'

const emit = defineEmits(['done'])

const form = reactive({ user: '', password: '', confirm: '' })
const busy = ref(false)
const error = ref('')

async function submit() {
  error.value = ''

  if (!form.user.trim()) {
    error.value = '请输入用户名'
    return
  }

  if (form.password.length < 8) {
    error.value = '密码至少 8 位'
    return
  }

  if (form.password !== form.confirm) {
    error.value = '两次输入的密码不一致'
    return
  }

  busy.value = true

  try {
    await initAccount(form.user.trim(), form.password)
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
      <p class="auth-sub">首次启动，创建管理员账户</p>

      <div class="field">
        <label>用户名</label>
        <input v-model="form.user" autocomplete="username" autofocus placeholder="3-32 位字母、数字、下划线、点、横线" />
      </div>

      <div class="field">
        <label>密码</label>
        <input v-model="form.password" type="password" autocomplete="new-password" placeholder="至少 8 位" />
      </div>

      <div class="field">
        <label>确认密码</label>
        <input v-model="form.confirm" type="password" autocomplete="new-password" />
      </div>

      <p v-if="error" class="error">{{ error }}</p>

      <button class="btn primary block" type="submit" :disabled="busy">
        {{ busy ? '创建中…' : '创建并登录' }}
      </button>

      <p class="auth-tip">初始化只能创建这一个账户，之后可以在面板里添加其他账户</p>
    </form>
  </div>
</template>
