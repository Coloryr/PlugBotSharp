<script setup>
import { reactive, ref, watch } from 'vue'
import { api } from '../api'
import { notify } from '../toast'

const props = defineProps({
  open: { type: Boolean, default: false },
  account: { type: Object, default: null },
  self: { type: Boolean, default: false }
})

const emit = defineEmits(['close', 'done'])

const form = reactive({ oldPassword: '', password: '', confirm: '' })
const busy = ref(false)
const error = ref('')

watch(
  () => props.open,
  open => {
    if (open) {
      form.oldPassword = ''
      form.password = ''
      form.confirm = ''
      error.value = ''
    }
  }
)

async function submit() {
  error.value = ''

  if (form.password.length < 8) {
    error.value = '密码至少 8 位'
    return
  }

  if (form.password !== form.confirm) {
    error.value = '两次输入的密码不一致'
    return
  }

  if (props.self && !form.oldPassword) {
    error.value = '请输入原密码'
    return
  }

  busy.value = true

  try {
    const body = { password: form.password }

    if (props.self) {
      body.oldPassword = form.oldPassword
    }

    const res = await api.post(`admin/accounts/${props.account.id}/password`, body)
    notify(res.message || '密码已修改', 'ok')
    emit('done')
    emit('close')
  } catch (e) {
    error.value = e.message
  } finally {
    busy.value = false
  }
}

function close() {
  if (!busy.value) {
    emit('close')
  }
}
</script>

<template>
  <div v-if="open" class="overlay" @click.self="close">
    <div class="dialog">
      <header class="dialog-header">
        <h3>{{ self ? '修改密码' : '重置密码' }}</h3>
        <button class="btn ghost" :disabled="busy" @click="close">关闭</button>
      </header>

      <p v-if="account" class="dialog-sub">账户：{{ account.user }}</p>

      <div v-if="self" class="field">
        <label>原密码</label>
        <input v-model="form.oldPassword" type="password" autocomplete="current-password" />
      </div>

      <div class="field" style="margin-top: 12px">
        <label>新密码</label>
        <input v-model="form.password" type="password" autocomplete="new-password" placeholder="至少 8 位" />
      </div>

      <div class="field" style="margin-top: 12px">
        <label>确认新密码</label>
        <input v-model="form.confirm" type="password" autocomplete="new-password" />
      </div>

      <p v-if="error" class="error" style="margin-top: 12px">{{ error }}</p>

      <footer class="dialog-footer">
        <button class="btn" :disabled="busy" @click="close">取消</button>
        <button class="btn primary" :disabled="busy" @click="submit">
          {{ busy ? '保存中…' : '保存' }}
        </button>
      </footer>
    </div>
  </div>
</template>
