<script setup>
import { ref, watch } from 'vue'
import { api } from '../api'
import { notify } from '../toast'

const props = defineProps({
  open: { type: Boolean, default: false },
  title: { type: String, default: '设置' },
  sub: { type: String, default: '' },
  note: { type: String, default: '' },
  path: { type: String, default: '' },
  reloadPath: { type: String, default: '' }
})

const emit = defineEmits(['close', 'done'])

const typeName = {
  int: '整数',
  short: '短整数',
  ushort: '无符号短整数',
  uint: '无符号整数',
  long: '长整数',
  ulong: '无符号长整数',
  float: '小数',
  double: '双精度小数',
  bool: '开关',
  string: '文本',
  list: '列表',
  kv: '键值',
  enum: '单选'
}

const integerTypes = ['int', 'short', 'ushort', 'uint', 'long', 'ulong']
const decimalTypes = ['float', 'double']

/**
 * 整数类型的输入框
 * @param {string} type 配置类型
 */
function isInteger(type) {
  return integerTypes.includes(type)
}

/**
 * 小数类型的输入框
 * @param {string} type 配置类型
 */
function isDecimal(type) {
  return decimalTypes.includes(type)
}

const items = ref([])
const loading = ref(false)
const busy = ref(false)
const error = ref('')

watch(
  () => [props.open, props.path],
  async ([open]) => {
    if (!open || !props.path) {
      return
    }

    items.value = []
    error.value = ''
    loading.value = true

    try {
      const res = await api.get(props.path)
      items.value = Object.entries(res.data || {}).map(([key, item]) => toEditable(key, item))
    } catch (e) {
      error.value = e.message
    } finally {
      loading.value = false
    }
  }
)

/**
 * 服务端返回的配置项转成表单能编辑的结构
 * @param {string} key 配置名
 * @param {object} item 配置项
 */
function toEditable(key, item) {
  const one = { ...item, key: item.key || key }

  if (one.type === 'list') {
    one.text = (one.value || []).join('\n')
  }

  if (one.type === 'kv') {
    one.rows = Object.entries(one.value || {}).map(([k, v]) => ({ k, v }))
  }

  return one
}

/**
 * 数值项是否带有效范围，min 和 max 都没填时不限制
 * @param {object} item 配置项
 */
function hasRange(item) {
  return typeof item.max === 'number' && item.max > item.min
}

function clamp(value, item) {
  if (!hasRange(item)) {
    return value
  }

  return Math.min(Math.max(value, item.min), item.max)
}

/**
 * 取表单项当前的值，按类型转换
 * @param {object} item 配置项
 */
function valueOf(item) {
  if (isInteger(item.type)) {
    const num = Number(item.value)
    return clamp(Number.isFinite(num) ? Math.trunc(num) : 0, item)
  }

  if (isDecimal(item.type)) {
    const num = Number(item.value)
    return clamp(Number.isFinite(num) ? num : 0, item)
  }

  switch (item.type) {
    case 'bool':
      return !!item.value
    case 'string':
    case 'enum':
      return String(item.value ?? '')
    case 'list': {
      const list = String(item.text ?? '')
        .split('\n')
        .map(x => x.trim())
        .filter(Boolean)

      return item.maxCount > 0 ? list.slice(0, item.maxCount) : list
    }
    case 'kv':
      return item.rows.reduce((acc, row) => {
        const key = String(row.k ?? '').trim()

        if (key) {
          acc[key] = String(row.v ?? '')
        }

        return acc
      }, {})
    default:
      return item.value
  }
}

/**
 * 组装提交体：去掉编辑用的临时字段，值按类型转换
 */
function buildBody() {
  const body = {}

  for (const item of items.value) {
    const { text, rows, ...rest } = item
    body[item.key] = { ...rest, value: valueOf(item) }
  }

  return body
}

/**
 * 恢复默认值
 * @param {object} item 配置项
 */
function reset(item) {
  item.value = item.default

  if (item.type === 'list') {
    item.text = (item.default || []).join('\n')
  }

  if (item.type === 'kv') {
    item.rows = Object.entries(item.default || {}).map(([k, v]) => ({ k, v }))
  }
}

/**
 * 下拉选项的显示文案，空值表示不启用
 * @param {string} value 选项值
 */
function optionLabel(value) {
  return value === '' ? '（不使用）' : value
}

function addRow(item) {
  item.rows.push({ k: '', v: '' })
}

function removeRow(item, index) {
  item.rows.splice(index, 1)
}

function defaultText(item) {
  if (item.type === 'bool') {
    return item.default ? '开启' : '关闭'
  }

  if (item.type === 'list') {
    return (item.default || []).join(' / ') || '空'
  }

  if (item.type === 'kv') {
    return Object.keys(item.default || {}).length + ' 项'
  }

  return item.default === undefined || item.default === null || item.default === '' ? '空' : String(item.default)
}

/**
 * 每项下面的约束说明
 * @param {object} item 配置项
 */
function hintOf(item) {
  const parts = []

  if ((isInteger(item.type) || isDecimal(item.type)) && hasRange(item)) {
    parts.push(`范围 ${item.min} ~ ${item.max}`)
  }

  if (item.type === 'string' && item.maxLength > 0) {
    parts.push(`最多 ${item.maxLength} 个字符`)
  }

  if (item.type === 'list' && item.maxCount > 0) {
    parts.push(`最多 ${item.maxCount} 项`)
  }

  parts.push('默认 ' + defaultText(item))

  return parts.join(' · ')
}

async function submit() {
  busy.value = true
  error.value = ''

  try {
    const res = await api.post(props.path, buildBody())

    if (props.reloadPath) {
      try {
        const reloaded = await api.post(props.reloadPath)
        notify(reloaded.message || '配置已保存并重载', 'ok')
      } catch (e) {
        notify('配置已保存，但重载失败：' + e.message, 'warn')
      }
    } else {
      notify(res.message || '配置已保存', 'ok')
    }

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
    <div class="dialog wide">
      <header class="dialog-header">
        <h3>{{ title }}</h3>
        <button class="btn ghost" :disabled="busy" @click="close">关闭</button>
      </header>

      <p v-if="sub" class="dialog-sub">{{ sub }}</p>
      <p v-if="note" class="hint-line">{{ note }}</p>

      <p v-if="loading" class="empty">加载中…</p>
      <p v-else-if="error && items.length === 0" class="error">{{ error }}</p>
      <p v-else-if="items.length === 0" class="empty">这个对象没有提供配置项</p>

      <div v-else class="cfg">
        <div v-for="item in items" :key="item.key" class="cfg-item">
          <div class="cfg-head">
            <span class="key mono">{{ item.key }}</span>
            <span class="tag">{{ typeName[item.type] || item.type }}</span>
            <button class="btn ghost sm" :disabled="busy" @click="reset(item)">默认值</button>
          </div>

          <p v-if="item.description" class="cfg-desc">{{ item.description }}</p>

          <input
            v-if="isInteger(item.type)"
            v-model.number="item.value"
            type="number"
            step="1"
            :min="item.min"
            :max="item.max"
            :placeholder="item.placeholder || undefined"
          />
          <input
            v-else-if="isDecimal(item.type)"
            v-model.number="item.value"
            type="number"
            step="0.01"
            :min="item.min"
            :max="item.max"
            :placeholder="item.placeholder || undefined"
          />
          <label v-else-if="item.type === 'bool'" class="switch">
            <input v-model="item.value" type="checkbox" />
            <span class="track"><span class="thumb" /></span>
            <span class="switch-text">{{ item.value ? '开启' : '关闭' }}</span>
          </label>
          <select v-else-if="item.type === 'enum'" v-model="item.value">
            <option v-for="opt in item.options || []" :key="opt" :value="opt">{{ optionLabel(opt) }}</option>
          </select>
          <input
            v-else-if="item.type === 'string'"
            v-model="item.value"
            :type="item.secret ? 'password' : 'text'"
            :maxlength="item.maxLength > 0 ? item.maxLength : undefined"
            :autocomplete="item.secret ? 'new-password' : 'off'"
            :placeholder="item.placeholder || undefined"
          />
          <textarea
            v-else-if="item.type === 'list'"
            v-model="item.text"
            rows="4"
            :placeholder="item.placeholder || '一行一项'"
          ></textarea>
          <div v-else-if="item.type === 'kv'" class="kv">
            <div v-for="(row, index) in item.rows" :key="index" class="kv-row">
              <input v-model="row.k" placeholder="键" />
              <input v-model="row.v" placeholder="值" />
              <button class="btn ghost sm" :disabled="busy" @click="removeRow(item, index)">删除</button>
            </div>
            <button class="btn sm" :disabled="busy" @click="addRow(item)">添加一行</button>
          </div>

          <p class="cfg-hint">{{ hintOf(item) }}</p>
        </div>
      </div>

      <p v-if="error && items.length > 0" class="error" style="margin-top: 12px">{{ error }}</p>

      <footer class="dialog-footer">
        <button class="btn" :disabled="busy" @click="close">取消</button>
        <button class="btn primary" :disabled="busy || loading || items.length === 0" @click="submit">
          {{ busy ? '保存中…' : '保存' }}
        </button>
      </footer>
    </div>
  </div>
</template>