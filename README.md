# Robbie's Vehicle Physics

A robust, custom Rigidbody vehicle controller for Unity. Unlike standard Unity `WheelColliders`, this system uses **Mesh Sweep Tests** to detect the ground, allowing for accurate collision with the actual shape of wheel.

## Key Features

* **Unlimited Contact Points:** A single wheel can rest on two different surfaces (e.g., bridging a gap) simultaneously.
* **Mesh-Based Collision:** The physics uses the actual 3D mesh of your wheel, not a primitive sphere or raycast.
* **Realistic Suspension:** Suspension using Hook's law.

---

## Installation

1.  **Create your Car:**
    * Create a root GameObject with a `Rigidbody`.
    * Set the Rigidbody mass (e.g., 1500).
2.  **Add the Scripts:**
    * Add `Vehicle.cs` to the root GameObject.
    * Add `Wheel.cs` to each of your your wheel GameObjects.
3.  **Setup Layers:**
    * Create a Layer named `Drivable` (or similar).
    * Assign this layer to your floors, ramps, and obstacles.
    * In the `Vehicle.cs` inspector, set **Drivable Layers** to this layer.
4.  **Configure Wheels:**
    * In `Vehicle.cs`, expand the **Wheels** list.
    * Add elements for each wheel (FL, FR, RL, RR).
    * Drag the corresponding `Wheel` GameObject into the **Wheel Script** slot.
    * Check `Is Steering` for front wheels and `Is Motor` for drive wheels.

---

## Configuration Guide

### Arcade Power & Handling
* **Acceleration Force:** Forward power (Newton-meters).
* **Brake Force:** Stopping power.
* **Steer Angle:** Maximum rotation of front wheels.
* **Tire Grip Factor:** Global traction multiplier (0.0 - 1.0).
* **Grip Curve:** Defines how the car drifts.
    * *X-Axis:* Slip Amount.
    * *Y-Axis:* Grip Multiplier.
    * *Tip:* A flat curve makes the car drive like a train. A curve that drops off quickly makes it drift easily.

### Suspension Settings
* **Suspension Rest Dist:** The length of the spring when fully extended (in meters).
* **Spring Stiffness:** How hard the spring pushes back. Higher = Sportier/Bouncier.
* **Damper Stiffness:** Resistance to movement. Higher = Less bounce, "heavier" feel.
* **Suspension Smoothing:** Visual smoothing for the wheel mesh (does not affect physics).

---

## Debugging in Builds
Unity's `OnDrawGizmos` does not work in built games. This project uses a custom `OnRenderObject` pipeline with `GL.LINES`.
* **Cyan Sphere:** Center of Mass.
* **White Sphere:** Suspension Anchor point.
* **Green/Blue Line:** The spring (changes color based on compression).
* **Green Ghost Wheel:** Visualizes exactly where the physics engine thinks the wheel is touching.

