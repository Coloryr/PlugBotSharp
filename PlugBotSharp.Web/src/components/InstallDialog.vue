<script setup>
import { ref, watch } from 'vue'
import { api } from '../api'
import { notify } from '../toast'

const props = defineProps({
  open: { type: Boolean, default: false }
})

const emit = defineEmits(['close', 'installed'])

const file = ref(null)
const localPath = ref('')
const busy = ref(false)
const dragging = ref(false)
const error = ref('')
const fileInput = ref(null)

watch(
  () => props.open,
  open => {
    if (open) {
      file.value = null
      localPath.value = ''
      error.value = ''
    }
  }
)

function onPick(event) {
  file.value = event.target.files?.[0] ?? null
  error.value = ''
}

function onDrop(event) {
  dragging.value = false
  const dropped = event.dataTransfer?.files?.[0]

  if (dropped) {
    file.value = dropped
    error.value = ''
  }
}

async function submit() {
  error.value = ''

  if (!file.value && !localPath.value.trim()) {
    error.value = '请选择 zip 文件，或者填写本地路径'
    return
  }

  busy.value = true

  try {
    const res = file.value
      ? await api.upload('admin/components/install', file.value)
      : await api.post('admin/components/install', { path: localPath.value.trim() })

    notify(res.message || '安装完成', 'ok')

    if (res.data && res.data.loaded === false) {
      notify('安装完成，但加载失败，请查看日志', 'warn')
    }

    file.value = null
    localPath.value = ''
    emit('installed')
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
        <h3>安装组件</h3>
        <button class="btn ghost" :disabled="busy" @click="close">关闭</button>
      </header>

      <div
        class="drop"
        :class="{ active: dragging }"
        @click="fileInput?.click()"
        @dragover.prevent="dragging = true"
        @dragleave.prevent="dragging = false"
        @drop.prevent="onDrop"
      >
        <div class="drop-title">{{ file ? file.name : '点击选择，或者把组件 zip 拖到这里' }}</div>
        <div class="drop-hint">压缩包内需要有 Meta.json 和清单里声明的 dll</div>
        <!-- 阻止冒泡，否则 div 的 click 会反复触发 input.click() -->
        <input ref="fileInput" type="file" accept=".zip" hidden @click.stop @change="onPick" />
      </div>

      <div class="field" style="margin-top: 14px">
        <label>或者从本地路径安装（zip 或目录）</label>
        <input v-model="localPath" placeholder="D:\build\DemoPlugin.zip" />
      </div>

      <p v-if="error" class="error">{{ error }}</p>

      <footer class="dialog-footer">
        <button class="btn" :disabled="busy" @click="close">取消</button>
        <button class="btn primary" :disabled="busy" @click="submit">
          {{ busy ? '安装中…' : '安装' }}
        </button>
      </footer>
    </div>
  </div>
</template>
