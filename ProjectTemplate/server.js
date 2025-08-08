const express = require('express');
const bodyParser = require('body-parser');
const mysql = require('mysql2');
const app = express();
const PORT = 3000;

app.use(bodyParser.json());
app.use(express.static(__dirname));

let currentUser = null;

const db = mysql.createPool({
    connectionLimit: 10,
    host: '107.180.1.16',
    port: 3306,
    user: 'cis440summer2025team5',
    password: 'cis440summer2025team5',
    database: 'cis440summer2025team5'
});


db.getConnection((err, connection) => {
    if (err) {
        console.error('❌ Initial DB connection failed:', err);
    } else {
        console.log(' Connected to MySQL database (pool)');
        connection.release();
    }
});


app.post('/logon', (req, res) => {
    const { uid, pass } = req.body;

    const query = 'SELECT * FROM users WHERE username = ? AND pass = ?';
    db.query(query, [uid, pass], (err, results) => {
        if (err) {
            console.error('❌ Query error:', err);
            return res.status(500).json(false);
        }

        if (results.length > 0) {
            const user = results[0];

            currentUser = {
                isAdmin: user.isAdmin === 1,
                username: user.username,
                empid: user.empid,
                dept: user.dept
            };

            res.json({
                success: true,
                ...currentUser
            });
        } else {
            res.json({ success: false });
        }
    });
});


app.post('/LoginInfo', (req, res) => {
    if (currentUser) {
        res.json(currentUser);
    } else {
        res.json({ isAdmin: false });
    }
});


app.listen(PORT, () => {
    console.log(`Server running at http://localhost:${PORT}`);
});