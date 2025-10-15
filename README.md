![Preview](Docs/fracture_demo.gif)

<h1 align="center">💥 Sprite Fracturer 2D for Unity</h1>

<p align="center">
A lightweight runtime 2D sprite fracturing system for Unity.<br/>
Split any sprite into physics-driven pieces with explosion force, blink effect, and cleanup options.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Unity-2022.3%2B-black?logo=unity" />
  <img src="https://img.shields.io/badge/License-MIT-green" />
  <img src="https://img.shields.io/badge/RenderPipeline-Built--in%2FURP%2FHDRP-blue" />
</p>

---

## 📦 Installation

1. Copy the folder `Assets/SpriteFracturer2D/` into your Unity project.  
2. Ensure your sprite texture is set to **Read/Write Enabled** (Unity Import Settings).  
3. Add the **SpriteFracturer2D** component to any GameObject with a **SpriteRenderer**.  

---

## ⚙️ Component Overview

### 🔹 **Trigger Mode**
Defines how the fracture starts:
- **AutoStart** – explodes automatically after a delay  
- **Collision** – explodes on physical impact  
- **Trigger** – explodes when entering a trigger collider  

### 🔹 **Fracture Settings**
- **Columns / Rows** → number of generated pieces  
- **Explosion Force / Upward Modifier** → controls the physics impulse  

### 🔹 **Destruction Settings**
- **Destroy Pieces** → auto-remove pieces after lifetime  
- **Destroy on Collision** → pieces break when colliding with the environment  
- **Use Blink** → makes pieces flicker before being destroyed  

### 🔹 **Extras**
- **Pieces as Trigger** → makes fragments non-blocking  
- **Show Grid Gizmos** → visualize the cut grid in Scene view  
- **Events** → callbacks on fracture and piece destruction  

---

## 🧩 Usage Example

No code needed.  
Simply attach the component to a GameObject with a **SpriteRenderer**, adjust your settings, and press **Play**.

---

## 🧠 Tips
- For best results, use sprites with transparent backgrounds.  
- Enable **Read/Write** in the sprite import settings.  
- Works perfectly with **URP 2D Renderer** and **Sprite-Lit-Default** material.  

---

## 📜 License
This project is released under the [MIT License](LICENSE).  
You are free to use, modify, and distribute it in personal or commercial projects.

---

<p align="center">Created with ❤️ by <b>PAREIN Jean-Philippe</b></p>
