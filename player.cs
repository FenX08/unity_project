// C# doesn't know how to #define for shit.
using UnityEngine;
using UnityEngine.InputSystem;

// This is to shorten input checks to just "Started" and "Waiting".
using static UnityEngine.InputSystem.InputActionPhase;
using static custom_functions;

public class player : MonoBehaviour {
    // Render stuff.
    Renderer                player_renderer;

    // Animation stuff.
    SkinnedMeshRenderer     player_mesh;
    Animator                animator;
    int                     bones_number;

    Transform[]             player_bones;
    Transform[]             bones_inactive;

    Transform               spine_02_bone;
    Transform               head_bone;
    Transform               eye_camera_bone;

    // Player values.
    Transform   player_transform;
    Rigidbody   player_rigidbody;
    BoxCollider player_box_collider;

    // I tried to use enum for flags, but C# doesn't even know what enum is, so, no enum I guess.
    uint PLAYER_MOVEMENT;
        const byte isInactive = 0;
        const byte isWalking = 1;
        const byte isSprinting = 2;
        const byte isJumping = 3;
        const byte isCrouching = 4;
        const byte isAirborne = 5;

    // float move_speed_default = 2.4f;
    float move_speed_default = 2.23f;
    // float move_speed_default = 1.7f;
    float move_speed_max = 0;
    float move_speed = 0;
    float move_speed_sinusoidal_lerp = 0;
    float jump_speed = 7.0f;
    float sprint_speed = 1.63f;
    float crouch_speed = 0.67f;
    float crouch_time_speed = 4.56f;
    float crouch_time_lerp = 0;
    float player_move_angle = 0;

    ContactPoint[] player_collision_contacts = new ContactPoint[64];
    
    bool found_ground = false;
    Vector3 physics_position = Vector3.zero;

    // We scale our player model in crouching state for now.
    Vector3 player_box_collider_center_standing;
    Vector3 player_box_collider_center_crouching;
    Vector3 player_box_collider_size_standing;
    Vector3 player_box_collider_size_crouching;
    Vector3 player_standing_scale;
    Vector3 player_crouching_scale;

    // Camera values.
    Camera      player_camera;
    Transform   camera_transform;
    Transform   camera_thirdperson_player_rotation_point_transform;
    GameObject  camera_thirdperson_player_rotation_point;

    uint CAMERA_VIEWMODE;
        const byte isFirstperson = 0;
        const byte isThirdperson = 1;

    // TODO: we should recalculate fov after window resizing.
    // Also, if we have ultrawide screen Vegnette effect on camera will break things (because color correction).
    // Just keep this in mind for the future.
    float default_fov = 104.0f;
    float camera_fov;
    float default_zoom_fov = 2.0f;
    float zoom_fov;

    float camera_move_angle_x = 0;
    float camera_move_angle_y = 0;

    Vector3 camera_position_thirdperson_offset = new Vector3(0, 3.0f, -3.7f);
        Vector3 camera_position_thirdperson_standing;
        Vector3 camera_position_thirdperson_crouching;

    Vector3 camera_position_thirdperson_point_offset = new Vector3(0, 1.7f, 0.0f);
    
    // These clamp values needs to be changed, if we change thirdperson_offset vectors.
    float   camera_move_angle_y_clamp_min = -65.0f;
    float   camera_move_angle_y_clamp_max = 105.0f;
    int     camera_collision_hit_count;

    RaycastHit[] camera_collision_hit_result = new RaycastHit[64];
    float   camera_to_player_distance_default;
    float   camera_to_player_distance_current;
    int     camera_layer_exclude_collision = 1 << 3; // For now camera will ignore player collision only.

    // Input values.
    InputAction     move_input;
    InputAction     jump_input;
    InputAction     crouch_input;
    InputAction     sprint_input;
    InputAction     camera_input;
    InputAction     camera_viewmode_input;
    InputAction     zoom_input;
    InputAction     time_control;

    bool    jump_pressed = false;
    bool    crouch_pressed = false;
    bool    sprint_pressed = false;
    bool    camera_viewmode_pressed = false;
    bool    zoom_pressed = false;
    bool    time_control_pressed = false;

    void Awake() {
        initialize_player();
        initialize_animation();
        initialize_camera();
        initialize_input();
    }

    void Start() {
        //
    }

    void FixedUpdate() {
        //
    }

    // Be carefull - Unity calls these physics functions several times in one frame for several collisions.
    void OnCollisionEnter(Collision collision) {
        ContactPoint contact;
        int contact_count = collision.GetContacts(player_collision_contacts);

        for (int i = 0; i < contact_count; ++i) {
            contact = player_collision_contacts[i];
            // float dot_product = Vector3.Dot(player_transform.forward, contact.normal);
            
            // Debug.Log(dot_product);
            // Debug.DrawLine(contact.point, contact.point + contact.normal, Color.green, 2, false);

            // Checks if the angle is good enough to be defined as ground.
            if (contact.normal.y >= Mathf.Sin(0.1f)) {
                PLAYER_MOVEMENT = CLEAR_BIT(PLAYER_MOVEMENT, isAirborne);
                PLAYER_MOVEMENT = CLEAR_BIT(PLAYER_MOVEMENT, isJumping);
                // Debug.DrawLine(contact.point, contact.point + contact.normal, Color.green, 2, false);
                break;
            }
        }
    }
    
    void OnCollisionStay(Collision collision) {
        ContactPoint contact;
        int contact_count = collision.GetContacts(player_collision_contacts);
        // bool found_wall_or_ceiling = false;

        for (int i = 0; i < contact_count; ++i) {
            contact = player_collision_contacts[i];

            // Checks if the angle is good enough to be defined as ground.
            if (contact.normal.y >= Mathf.Sin(0.1f)) {
                PLAYER_MOVEMENT = CLEAR_BIT(PLAYER_MOVEMENT, isAirborne);
                PLAYER_MOVEMENT = CLEAR_BIT(PLAYER_MOVEMENT, isJumping);
                found_ground = true;
                // Debug.DrawRay(contact.point, contact.normal * 10, Color.white);
                break;
            }

            // We can use this to check for walls and ceilings.
            // if (contact.normal.y <= Mathf.Sin(0.1f)) {
            //     found_wall_or_ceiling = true;
            //     Debug.DrawRay(contact.point, contact.normal * 10, Color.white);
            // }
        }
    }

    // void OnCollisionExit(Collision collision) {
    //     print("No longer in contact with " + collision.transform.name);
    // }

    void Update() {
        // Set "Process Events Manually" in Project Settings >> Input System Package >> Update Mode.
        // InputSystem.Update();
        
        player_move_update();
        animation_update();
        camera_update();
        zoom_logic();

        // debug_player_state();

        // Debug.Log($"{camera_input.ReadValue<Vector2>().x}, {camera_input.ReadValue<Vector2>().y}");
        // Debug.Log("Player state is: " + System.Convert.ToString(PLAYER_MOVEMENT, 2)); // Binary conversion.
        // Debug.Log(QualitySettings.maxQueuedFrames);
    }

    void LateUpdate() {
        time_control_logic();
    }

    void player_move_update() {
        // Sinusoidal movement.
        // TODO: a mess initially, but I need to work around this with walk animation.
        // Think about the camera in firstperson and especially thirdperson, probably need to asign
        // the sinusoidal movement to thirdperson camera and lerp the camera movement.
        // Maybe assign the thirdperson camera to eye_bone?
        if (move_speed_sinusoidal_lerp > 1.0f - Mathf.Epsilon) {
            move_speed_sinusoidal_lerp = 0;
        }

        float move_speed_sinusoidal = move_speed * 0.97f;
        move_speed_sinusoidal_lerp += 1.0f * Time.deltaTime;

        float x = move_speed_sinusoidal_lerp;
        // Two tau, because of the leg steps in walk cycle.
        float t = 1 - (-(Mathf.Cos(TAU*2*x))) * 0.5f - 0.5f;
        move_speed = Mathf.Lerp(move_speed_sinusoidal, move_speed, t);

        // Debug.Log(move_speed);

        // We check if we found ground after all physics calculation.
        // If yes, just ignore and set found_ground to false for physics to check the ground in the next frame.
        if (!found_ground) {
            PLAYER_MOVEMENT = SET_BIT(PLAYER_MOVEMENT, isAirborne);
        }

        found_ground = false;

        // Crouch.
        // BUG: if you'll release crouch button in tight place (like vents), player will go through collision.
        if (crouch_input.phase == Started && !crouch_pressed) {
            PLAYER_MOVEMENT = SET_BIT(PLAYER_MOVEMENT, isCrouching);
            crouch_pressed = true;
            move_speed_max -= crouch_speed;
        } else if (crouch_input.phase == Waiting && crouch_pressed) {
            PLAYER_MOVEMENT = CLEAR_BIT(PLAYER_MOVEMENT, isCrouching);
            crouch_pressed = false;
            move_speed_max += crouch_speed;
        }

        // Sprint.
        if (sprint_input.phase == Started && !sprint_pressed) {
            PLAYER_MOVEMENT = SET_BIT(PLAYER_MOVEMENT, isSprinting);
            sprint_pressed = true;
            move_speed_max += sprint_speed;
        } else if (sprint_input.phase == Waiting && sprint_pressed) {
            PLAYER_MOVEMENT = CLEAR_BIT(PLAYER_MOVEMENT, isSprinting);
            sprint_pressed = false;
            move_speed_max -= sprint_speed;
        }

        // Walk.
        if (move_input.phase == Started) {
            PLAYER_MOVEMENT = SET_BIT(PLAYER_MOVEMENT, isWalking);

            // Take into account small inputs from gamepad's stick.
            // BUG: doesn't work.
            // move_speed_max *= Mathf.Clamp01(move_input.ReadValue<Vector2>().magnitude);
            
            // Speed gradually accelerates.
            // TODO: How do we save speed from walking to jumping and airborne states?
            move_speed += move_speed_max * 2.7f * Time.deltaTime;

            // Clamp speed depending on other movement states.
            // TODO: How do we gradually slow down?
            if (move_speed > move_speed_max) {
                move_speed = move_speed_max;
            }

            // Receive move input.
            float move_input_x = move_input.ReadValue<Vector2>().x;
            float move_input_y = move_input.ReadValue<Vector2>().y;

            if (CAMERA_VIEWMODE == isFirstperson) {
                player_transform.Translate(move_input_x * move_speed * Time.deltaTime, 0, move_input_y * move_speed * Time.deltaTime);
            } else if (CAMERA_VIEWMODE == isThirdperson) {
                // Player moves depending on camera position, this method is opposite to tank controls.
                player_transform.Translate(move_input_x * move_speed * Time.deltaTime, 0, move_input_y * move_speed * Time.deltaTime, camera_thirdperson_player_rotation_point_transform);
                float input_angle = Mathf.Atan2(-move_input_x, move_input_y) * Mathf.Rad2Deg;
                player_move_angle = camera_move_angle_x - input_angle;
            }
        } else if (move_input.phase == Waiting) {
            PLAYER_MOVEMENT = CLEAR_BIT(PLAYER_MOVEMENT, isWalking);

            // Set the speed to zero very fast. Probably will not need this later.
            if (move_speed > Mathf.Epsilon) {
                move_speed -= move_speed_default * 10.0f * Time.deltaTime;
            } else {
                move_speed = 0;
            }
        }

        // Jump.
        // We set jumping state for one frame only, it get's cleared by collision detection.
        if (!IS_BIT_SET(PLAYER_MOVEMENT, isJumping) && !IS_BIT_SET(PLAYER_MOVEMENT, isAirborne)) {
            if (jump_input.phase == Started && !jump_pressed) {
                PLAYER_MOVEMENT = SET_BIT(PLAYER_MOVEMENT, isJumping);
                jump_pressed = true;
                player_rigidbody.AddForce(0, jump_speed, 0, ForceMode.VelocityChange);
            } else if (jump_input.phase == Waiting && jump_pressed) {
                jump_pressed = false;
            }
        }

        // Debug.DrawLine(player_transform.position, player_transform.position + (player_transform.forward * 10), Color.cyan, 0.0f, false);

        // Lerp different states.

        // Move lerp in thirdperson.
        // In firstperson we rotate character in camera_update_rotation_firstperson().
        if (IS_BIT_SET(PLAYER_MOVEMENT, isWalking) && CAMERA_VIEWMODE == isThirdperson) {
            player_transform.rotation = Quaternion.Lerp(
                player_transform.rotation,
                Quaternion.AngleAxis(player_move_angle, Vector3.up),
                (3.7f * 2) * Time.deltaTime
            );
        }

        // Crouch lerp.
        // TODO: is this the best way to lerp crouch state?
        // Later: we will add animations and camera will follow eye_camera bone,
        // and we will get rid of "player_transform.localScale".
        if (IS_BIT_SET(PLAYER_MOVEMENT, isCrouching)) {            
            if (crouch_time_lerp < 1.0f - Mathf.Epsilon) {
                crouch_time_lerp += crouch_time_speed * Time.deltaTime;

                player_box_collider.center = Vector3.Lerp(player_box_collider_center_standing, player_box_collider_center_crouching, crouch_time_lerp);
                player_box_collider.size = Vector3.Lerp(player_box_collider_size_standing, player_box_collider_size_crouching, crouch_time_lerp);
                player_transform.localScale = Vector3.Lerp(player_standing_scale, player_crouching_scale, crouch_time_lerp);
            } else {
                player_box_collider.center = player_box_collider_center_crouching;
                player_box_collider.size = player_box_collider_size_crouching;
                player_transform.localScale = player_crouching_scale;
            }
        } else {
            if (crouch_time_lerp > Mathf.Epsilon) {
                crouch_time_lerp -= crouch_time_speed * Time.deltaTime;

                player_box_collider.center = Vector3.Lerp(player_box_collider_center_standing, player_box_collider_center_crouching, crouch_time_lerp);
                player_box_collider.size = Vector3.Lerp(player_box_collider_size_standing, player_box_collider_size_crouching, crouch_time_lerp);
                player_transform.localScale = Vector3.Lerp(player_standing_scale, player_crouching_scale, crouch_time_lerp);
            } else {
                player_box_collider.center = player_box_collider_center_standing;
                player_box_collider.size = player_box_collider_size_standing;
                player_transform.localScale = player_standing_scale;
            }
        }

        // WARNING: State number is hardcoded - do not forget to change it, if you add new states.
        // Check if we are just standing still. We are counting from 1, because we do not need to count from Inactive state.
        // TODO: make this not hardcoded, I guess?
        for (byte i = 1; i < 6; ++i) {
            if (IS_BIT_SET(PLAYER_MOVEMENT, i)) {
                PLAYER_MOVEMENT = CLEAR_BIT(PLAYER_MOVEMENT, isInactive);
                break;
            }
            
            if (i == 5) {
                PLAYER_MOVEMENT = SET_BIT(PLAYER_MOVEMENT, isInactive);
            }
        }
    }

    void animation_update() {
        // animator.Play("anim.idle", 0, 0.25f);
    }

    // BUG: Rotation "drifts" sometimes, check deadzones in Project Settings -> Input System.
    // Also, do we need to change camera rotation speed with fixedDeltaTime?
    void camera_update() {
        if (camera_viewmode_input.phase == Started && !camera_viewmode_pressed) {
            camera_viewmode_pressed = true;
            
            if (CAMERA_VIEWMODE == isFirstperson) {
                // BUG: Look at the floor in firstperson, then change the view.
                // Need to find a way to change rotation without this behaviour.
                CAMERA_VIEWMODE = isThirdperson;

                // player_renderer.enabled = true;
            } else {
                CAMERA_VIEWMODE = isFirstperson;

                // For now we disable player's mesh in firstperson view.
                // player_renderer.enabled = false;
            }
        } else if (camera_viewmode_input.phase == Waiting && camera_viewmode_pressed) {
            camera_viewmode_pressed = false;
        }

        if (CAMERA_VIEWMODE == isFirstperson) {
            camera_update_rotation_firstperson();
            camera_update_position_firstperson();
        } else if (CAMERA_VIEWMODE == isThirdperson) {
            camera_update_rotation_thirdperson();
            camera_update_position_thirdperson();
        }
    }

    void camera_update_rotation_firstperson() {
        if (camera_input.phase == Started) {
            // Add user customized sensitivity later.
            float mouse_sensitivity = 100.0f;

            float input_value_x = camera_input.ReadValue<Vector2>().x;
            float input_value_y = camera_input.ReadValue<Vector2>().y;

            camera_move_angle_x += input_value_x * mouse_sensitivity;
            camera_move_angle_y += input_value_y * mouse_sensitivity;
		    camera_move_angle_y = Mathf.Clamp(camera_move_angle_y, -90.0f, 90.0f);
            
            if (camera_move_angle_x > 360.0f || camera_move_angle_x < -360.0f) {
                camera_move_angle_x %= 360.0f;
            }
            if (camera_move_angle_y > 360.0f || camera_move_angle_y < -360.0f) {
                camera_move_angle_y %= 360.0f;
            }
        }
        
        // Unity's lerp is nlerp actually.
        camera_transform.rotation = Quaternion.Lerp(
            camera_transform.rotation,
            Quaternion.AngleAxis(camera_move_angle_x, Vector3.up) * Quaternion.AngleAxis(camera_move_angle_y, Vector3.left),
            1.0f
        );

        player_transform.rotation = Quaternion.Lerp(
            player_transform.rotation,
            Quaternion.AngleAxis(camera_move_angle_x, Vector3.up),
            1.0f
        );

        // Let the bones follow the camera. 
        // Dividing camera_move_angle_y by half constrains the head rotation.
        head_bone.localRotation = Quaternion.Lerp(head_bone.localRotation, Quaternion.AngleAxis(camera_move_angle_y * 0.5f, Vector3.left), 1.0f);

        // TODO: If we use flashlight in firstperson, update it it's rotation with camera.
        // If it's thirdperson - set it to player model / animation.
    }

    void camera_update_position_firstperson() {
        // Set camera to eye_camera bone position.
        camera_transform.position = eye_camera_bone.position;

        // Later: make an option to disable camera wobbling by assigning the camera to collision and not the bone.
        // 1.8 (collider height) * 0.0555 (equals 0.0999; 1.8 - 0.0999 = 1.7001 - this is approximately where eyes at for eye_camera_bone)
        // Debug.DrawLine(camera_transform.position, player_transform.position, new Color(0.99f, 0.56f, 0.09f, 1.0f), 0);

        // Debug player camera.
        // camera_transform.position = eye_camera_bone.TransformPoint(Vector3.back * 0.4f);
    }

    void camera_update_rotation_thirdperson() {
        if (camera_input.phase == Started) {
            // Add user customized sensitivity later.
            float mouse_sensitivity = 60.0f;

            float input_value_x = camera_input.ReadValue<Vector2>().x;
            float input_value_y = camera_input.ReadValue<Vector2>().y;

            camera_move_angle_x += input_value_x * mouse_sensitivity;
            camera_move_angle_y += input_value_y * mouse_sensitivity;

		    camera_move_angle_y = Mathf.Clamp(camera_move_angle_y, camera_move_angle_y_clamp_min, camera_move_angle_y_clamp_max);
            
            if (camera_move_angle_x > 360.0f || camera_move_angle_x < -360.0f) {
                camera_move_angle_x %= 360.0f;
            }
            if (camera_move_angle_y > 360.0f || camera_move_angle_y < -360.0f) {
                camera_move_angle_y %= 360.0f;
            }
        }

        // Unity's lerp is nlerp actually.
        camera_transform.rotation = Quaternion.Lerp(
            camera_transform.rotation,
            Quaternion.AngleAxis(camera_move_angle_x, Vector3.up) * Quaternion.AngleAxis(camera_move_angle_y, Vector3.left),
            1.0f
        );

        camera_thirdperson_player_rotation_point_transform.rotation = Quaternion.Lerp(
            camera_thirdperson_player_rotation_point_transform.rotation,
            Quaternion.AngleAxis(camera_move_angle_x, Vector3.up),
            1.0f
        );

        // // This works if we are doing aiming for example.
        // // We are doing rotations in player_move_update.
        // player_transform.rotation = Quaternion.Lerp(
        //     player_transform.rotation,
        //     Quaternion.AngleAxis(camera_move_angle_x, Vector3.up),
        //     1.0f
        // );
    }

    void camera_update_position_thirdperson() {
        // Move camera with updated rotation.
        camera_transform.position = player_transform.position + camera_position_thirdperson_offset;

        // We need to update ZY and ZX position with 2D point rotation formula.
        // Execution order of those positions matters. First we rotate ZY, then ZX.
        Vector3 camera_position_move = camera_transform.position;

        Vector3 player_position = player_transform.position + camera_position_thirdperson_point_offset; // This is the point we rotate around.
        Vector3 camera_new_vector = camera_position_move - player_position;
        Vector3 camera_move_vector = Vector3.zero;

        float camera_cosine_angle_axis_x = Mathf.Cos(camera_move_angle_x * Mathf.Deg2Rad);
        float camera_sine_angle_axis_x = Mathf.Sin(camera_move_angle_x * Mathf.Deg2Rad);

        float camera_cosine_angle_axis_y = Mathf.Cos(camera_move_angle_y * Mathf.Deg2Rad);
        float camera_sine_angle_axis_y = Mathf.Sin(camera_move_angle_y * Mathf.Deg2Rad);

        // Update ZY position on Y axis rotation.
        camera_move_vector.z = (camera_new_vector.z * camera_cosine_angle_axis_y) - (camera_new_vector.y * camera_sine_angle_axis_y);
        camera_move_vector.y = (camera_new_vector.z * camera_sine_angle_axis_y) + (camera_new_vector.y * camera_cosine_angle_axis_y);

        camera_position_move.z = camera_move_vector.z + player_position.z;
        camera_position_move.y = camera_move_vector.y + player_position.y;

        // Update ZX position on X axis rotation.
        camera_new_vector.z = camera_position_move.z - player_position.z;

        camera_move_vector.z = (camera_new_vector.z * camera_cosine_angle_axis_x) - (camera_new_vector.x * camera_sine_angle_axis_x);
        camera_move_vector.x = (camera_new_vector.z * camera_sine_angle_axis_x) + (camera_new_vector.x * camera_cosine_angle_axis_x);

        camera_position_move.z = camera_move_vector.z + player_position.z;
        camera_position_move.x = camera_move_vector.x + player_position.x;
        
        // Update camera position after all rotations.
        camera_transform.position = camera_position_move;
        camera_transform.LookAt(player_position);

        // Debug.Log($"{camera_position_move.x}, {camera_position_move.y}, {camera_position_move.z}");

        // Now we will detect if camera hit something.
        // Later: we can detect if we hit a player and make player model texture have effect of dithering.
        // You have to write your own shader for that.
        RaycastHit camera_collision = new RaycastHit();
        camera_collision.distance = Mathf.Infinity;
        float camera_collision_radius = 0.2f;
        
        // Should we check if we got any hits at all?
        camera_collision_hit_count = Physics.SphereCastNonAlloc(player_position, camera_collision_radius, -(camera_transform.forward), camera_collision_hit_result, camera_to_player_distance_default, camera_layer_exclude_collision, QueryTriggerInteraction.Ignore);
        
        for (int i = 0; i < camera_collision_hit_count; ++i) {
            if (camera_collision_hit_result[i].distance < camera_collision.distance && camera_collision_hit_result[i].distance > 0) {
                camera_collision = camera_collision_hit_result[i];
                // Debug.Log(camera_collision_hit_result[i].transform.position);
            }
        }

        if (camera_collision.distance < Mathf.Infinity) {
            camera_to_player_distance_current = Mathf.Lerp(camera_to_player_distance_current, camera_collision.distance, 1.0f - Mathf.Exp(-(1000.0f * Time.deltaTime)));
            camera_transform.position = player_position - ((camera_transform.rotation * Vector3.forward) * camera_to_player_distance_current);
        } else {
            camera_to_player_distance_current = Mathf.Lerp(camera_to_player_distance_current, camera_to_player_distance_default, 1.0f - Mathf.Exp(-(1000.0f * Time.deltaTime)));
            camera_transform.position = player_position - ((camera_transform.rotation * Vector3.forward) * camera_to_player_distance_current);
        }
    }

    void initialize_player() {
        player_renderer = GetComponent<Renderer>();
        player_mesh = GetComponent<SkinnedMeshRenderer>();
        player_transform = GetComponent<Transform>();
        player_rigidbody = GetComponent<Rigidbody>();
        player_box_collider = GetComponent<BoxCollider>();

        CAMERA_VIEWMODE = isThirdperson; // We start in firstperson view.
        PLAYER_MOVEMENT = isInactive;

        player_renderer.enabled = true;
        player_rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        player_rigidbody.drag = 1;

        move_speed_max = move_speed_default;
        
        // Crouch stuff.
        player_box_collider_center_standing = player_box_collider.center;
        player_box_collider_size_standing = player_box_collider.size;
        
        player_box_collider_center_crouching = new Vector3(
            player_box_collider_center_standing.x,
            player_box_collider_center_standing.y / 2,
            player_box_collider_center_standing.z
        );

        player_box_collider_size_crouching = new Vector3(
            player_box_collider_size_standing.x,
            player_box_collider_size_standing.y / 2,
            player_box_collider_size_standing.z
        );
        
        player_standing_scale = player_transform.localScale;

        player_crouching_scale = new Vector3(
            player_transform.localScale.x,
            player_transform.localScale.y / 2,
            player_transform.localScale.z
        );
    }

    void initialize_animation() {
        animator = GetComponent<Animator>();

        // We are looking for number of bones in our player model;
        bones_number = (int)player_mesh.bones.Length;

        // Initialize array for skeleton and GET DA BONES from there.
        player_bones = new Transform[bones_number];
        player_bones = player_mesh.bones;

        // Find da bones for other manipulations.
        for (int i = 0; i < bones_number; ++i) {
            if (player_bones[i].name == "spine_02") {
                spine_02_bone = player_bones[i];
            }

            if (player_bones[i].name == "head") {
                head_bone = player_bones[i];
            }

            if (player_bones[i].name == "eye_camera") {
                eye_camera_bone = player_bones[i];
            }
        }

        // for (int i = 0; i < bones_number; ++i) {
        //     Debug.Log(player_bones[i].name + " :: " + player_bones[i].position);
        // }
    }
    
    void initialize_camera() {
        // Later: we need some kind of callback function that checks if we changed window size,
        // so that we could recalculate camera fov.
        game_settings.set_default_window_settings();

        player_camera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        camera_transform = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Transform>();

        // Move camera position to 0.0.0, because if it's not at world origin (for some reason,
        // for example: if it's somewhere at wrong position in the Scene), the camera position on character will be wrong.
        camera_transform.position = Vector3.zero;

        // We do this, so we could use transform.Translate for player in thirdperson walking state.
        camera_thirdperson_player_rotation_point = new GameObject("Camera Thirdperson Player Rotation Point");
        camera_thirdperson_player_rotation_point_transform = camera_thirdperson_player_rotation_point.GetComponent<Transform>();
        
        camera_fov = Camera.HorizontalToVerticalFieldOfView(default_fov, player_camera.aspect);
        zoom_fov = Camera.HorizontalToVerticalFieldOfView(default_zoom_fov, player_camera.aspect);
        player_camera.fieldOfView = camera_fov;
        
        // If we'll add something like scroll-zoom, we need to recalculate the distance every frame.
        // For now, let's just do it once.
        // This is used for camera collision.
        camera_to_player_distance_default = Vector3.Distance(camera_transform.position, player_transform.position + camera_position_thirdperson_point_offset);
        camera_to_player_distance_current = camera_to_player_distance_default;
        camera_layer_exclude_collision = ~camera_layer_exclude_collision;

        // Crouch stuff.
        camera_position_thirdperson_standing = camera_position_thirdperson_offset;

        camera_position_thirdperson_crouching = new Vector3(
            camera_position_thirdperson_offset.x,
            camera_position_thirdperson_offset.y / 2,
            camera_position_thirdperson_offset.z
        );
    }

    // Probably should set this stuff through "Assets/Settings/player_controls.inputactions -> Edit asset"
    // I changed deadzones in settings to "Min 0.2" and "Max 0.9".
    // Later: maybe make this customizable.
    void initialize_input() {
        var map = new InputActionMap("Move Controller");

        // We add input variables to the "map".
        move_input = map.AddAction("Move", binding: "<Gamepad>/leftStick");
        jump_input = map.AddAction("Jump", binding: "<Keyboard>/Space");
        crouch_input = map.AddAction("Crouch", binding: "<Keyboard>/LeftCtrl");
        sprint_input = map.AddAction("Sprint", binding: "<Keyboard>/LeftShift");
        
        camera_input = map.AddAction("Camera Rotation", binding: "<Mouse>/delta", processors: "scaleVector2(x=0.001, y=0.001)");
        camera_viewmode_input = map.AddAction("Change camera view", binding: "<Keyboard>/F");
        zoom_input = map.AddAction("Zoom", binding: "<Mouse>/rightButton");
        time_control = map.AddAction("Slow Motion");

        // We assign our physical inputs to input variables.
        move_input.AddCompositeBinding("Dpad")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");


        jump_input.AddBinding("<Gamepad>/buttonSouth");
        crouch_input.AddBinding("<Gamepad>/buttonWest");
        crouch_input.AddBinding("<Keyboard>/C");
        sprint_input.AddBinding("<Gamepad>/buttonEast");
        camera_input.AddBinding("<Gamepad>/rightStick").WithProcessor("scaleVector2(x=0.02, y=0.02)");
        camera_viewmode_input.AddBinding("<Gamepad>/rightStickButton"); // BUG: Doesn't work.
        zoom_input.AddBinding("<Gamepad>/leftTrigger");
        time_control.AddCompositeBinding("Dpad").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow");

        // We enable our inputs.
        move_input.Enable();
        jump_input.Enable();
        crouch_input.Enable();
        sprint_input.Enable();
        camera_input.Enable();
        camera_viewmode_input.Enable();
        zoom_input.Enable();
        time_control.Enable();
    }

    // BUG: That is not how lerp works, fix it.
    void zoom_logic() {
        if (zoom_input.phase == Started) {
            zoom_pressed = true;

            player_camera.fieldOfView = Mathf.Lerp(player_camera.fieldOfView, zoom_fov, Time.deltaTime * 2.13f);
        }
        
        if (camera_viewmode_input.phase == Waiting && zoom_pressed) {
            player_camera.fieldOfView = Mathf.Lerp(player_camera.fieldOfView, camera_fov, Time.deltaTime * 2.64f);

            if (player_camera.fieldOfView >= camera_fov - Mathf.Epsilon) {
                player_camera.fieldOfView = camera_fov;
                zoom_pressed = false;
            }
        }
    }

    void time_control_logic() {
        if (time_control.phase == Started && !time_control_pressed) {
            time_control_pressed = true;

            if (time_control.ReadValue<Vector2>().y > 0) {
                Time.timeScale += 0.1f;

                if (Time.timeScale >= 1.0f) {
                    Time.timeScale = 1.0f;
                }

                Debug.Log("Time speedup! Time scale: " + Time.timeScale);
            } else if (time_control.ReadValue<Vector2>().y < 0) {
                Time.timeScale -= 0.1f;

                if (Time.timeScale <= 0.1f) {
                    Time.timeScale = 0.1f;
                }
                
                Debug.Log("Time slowdown! Time scale: " + Time.timeScale);
            }
        } else if (time_control.phase == Waiting && time_control_pressed) {
            time_control_pressed = false;
        }
    }

    void debug_player_state() {
        string[] player_states = {"isInactive", "isWalking", "isSprinting", "isJumping", "isCrouching", "isAirborne"};
        string player_states_result = "";

        for (byte i = 0; i < 6; ++i) {
            if (IS_BIT_SET(PLAYER_MOVEMENT, i)) {
                player_states_result += player_states[i] + " --- ";
            }
        }

        Debug.Log(player_states_result);
    }
}