<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { api } from '../api'
import { notify } from '../toast'

const instances = ref([])
const proxies = ref([])
const loading = ref(false)
const error = ref('')
const busy = ref('')
const form = ref({ proxy: '', name: '' })

const availableProxies = computed(() => proxies.value.filter(x => x.type === 'Proxy' && x.loaded && x.enabled))

const stateName = { enabled: '运行中', disabled: '已禁用', unloaded: '未加载' }

/**
 * 拉取实例和组件列表
 */
async function refresh() {
  loading.value = true

  try {
    const [instanceRes, componentRes] = await Promise.all([
      api.get('admin/instances'),
      api.get('admin/components')
    ])

    instances.value = instanceRes.data || []
    proxies.value = componentRes.data || []
    error.value = ''

    if (!form.value.proxy && availableProxies.value.length > 0) {
      form.value.proxy = availableProxies.value[0].id
    }
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

/**
 * 创建实例
 */
async function create() {
  if (!form.value.proxy) {
    notify('没有可用的代理，先加载并启用一个代理组件', 'warn')
    return
  }

  busy.value = 'create'

  try {
    const res = await api.post('admin/instances', { proxy: form.value.proxy, name: form.value.name })
    notify(res.message || '实例已创建', 'ok')

    if (res.data && res.data.warning) {
      notify(res.data.warning, 'warn')
    }

    form.value.name = ''
  } catch (e) {
    notify('创建失败：' + e.message, 'bad')
  } finally {
    busy.value = ''
    await refresh()
  }
}

/**
 * 实例操作：load / enable / disable
 * @param {object} item 实例
 * @param {string} action 动作
 * @param {string} label 按钮文案
 */
async function act(item, action, label) {
  busy.value = item.uuid + ':' + action

  try {
    const res = await api.post(`admin/instances/${item.uuid}/${action}`)
    notify(res.message || label + '成功', 'ok')
  } catch (e) {
    notify(label + '失败：' + e.message, 'bad')
  } finally {
    busy.value = ''
    await refresh()
  }
}

/**
 * 删除实例
 * @param {object} item 实例
 */
async function remove(item) {
  if (!confirm(`确定删除实例 ${item.name || item.uuid}？\n\n实例目录会被删除。`)) {
    return
  }

  busy.value = item.uuid + ':delete'

  try {
    const res = await api.del(`admin/instances/${item.uuid}`)
    notify(res.message || '已删除', 'ok')
  } catch (e) {
    notify('删除失败：' + e.message, 'bad')
  } finally {
    busy.value = ''
    await refresh()
  }
}

let timer = null

onMounted(() => {
  refresh()
  timer = setInterval(refresh, 5000)
})

onBeforeUnmount(() => {
  if (timer) {
    clearInterval(timer)
  }
})
</script>

<template>
  <section class="section">
    <div class="toolbar align-end">
      <div class="field grow">
        <label>代理组件</label>
        <select v-model="form.proxy" :disabled="availableProxies.length === 0">
          <option v-if="availableProxies.length === 0" value="">没有可用的代理</option>
          <option v-for="item in availableProxies" :key="item.id" :value="item.id">
            {{ item.name || item.id }} ({{ item.id }})
          </option>
        </select>
      </div>

      <div class="field grow">
        <label>实例名称</label>
        <input v-model="form.name" placeholder="例如 主账号" @keyup.enter="create" />
      </div>

      <button class="btn primary" :disabled="busy === 'create'" @click="create">创建实例</button>
    </div>

    <p v-if="availableProxies.length === 0" class="hint-line">
      代理组件需要先加载并启用，才能创建实例。
    </p>

    <p v-if="error" class="error">{{ error }}</p>

    <div v-if="instances.length === 0" class="empty">
      {{ loading ? '加载中…' : '还没有实例' }}
    </div>

    <table v-else class="table">
      <thead>
        <tr>
          <th>实例</th>
          <th>代理</th>
          <th>状态</th>
          <th class="right">操作</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="item in instances" :key="item.uuid">
          <td>
            <div class="name">{{ item.name || '未命名' }}</div>
            <div class="meta mono" :title="item.folder">{{ item.uuid }}</div>
          </td>
          <td><span class="tag">{{ item.proxy }}</span></td>
          <td>
            <span class="state" :class="item.state">{{ stateName[item.state] || item.state }}</span>
          </td>
          <td class="actions">
            <button v-if="!item.loaded" class="btn" :disabled="!!busy" @click="act(item, 'load', '加载')">
              加载
            </button>
            <button
              v-else
              class="btn"
              :disabled="!!busy"
              @click="act(item, item.enabled ? 'disable' : 'enable', item.enabled ? '禁用' : '启用')"
            >
              {{ item.enabled ? '禁用' : '启用' }}
            </button>

            <button class="btn danger" :disabled="!!busy" @click="remove(item)">删除</button>
          </td>
        </tr>
      </tbody>
    </table>
  </section>
</template>
