const express = require('express')
const app = express()
const port = 5000

app.get('/hello', (_req, res) => {
  res.send('Hello from Express!')
})

app.listen(port, () => {
  console.log(`Example app listening on port ${port}`)
})